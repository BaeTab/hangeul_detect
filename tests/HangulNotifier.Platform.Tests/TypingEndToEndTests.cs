using FluentAssertions;
using HangulNotifier.Core.Buffer;
using HangulNotifier.Core.Rules;
using HangulNotifier.Platform.Input;

namespace HangulNotifier.Platform.Tests;

/// <summary>
/// 키 입력부터 감지까지의 전 구간(end-to-end) 검증.
///
/// <para>기존 규칙 테스트는 완성된 문자열을 <c>RuleEngine.Check</c>에 직접 넘긴다. 그래서
/// 규칙 자체는 검증되지만 <b>실제로 키를 눌렀을 때</b> 그 문자열이 만들어지는지는 검증되지 않았다.
/// 이 구간(KeyTranslator → 오토마타 → WordBuffer)이 깨지면 규칙 테스트는 전부 통과하는데
/// 앱은 현장에서 아무것도 감지하지 못한다.</para>
///
/// <para>여기서는 두벌식 키 순서를 만들어 실제 파이프라인과 같은 순서로 흘려보내고,
/// 규칙에 달린 examples가 <b>타이핑으로도</b> 감지되는지 확인한다.</para>
/// </summary>
public class TypingEndToEndTests
{
    private static readonly IReadOnlyList<Rule> AllRules = RuleSet.LoadDefault().Rules;

    private static RuleEngine AllLevelsEngine() =>
        new(AllRules, new RuleEngineOptions { EnableCertain = true, EnableSuspect = true, EnableInfo = true });

    /// <summary>
    /// 키 순서를 실제 파이프라인과 동일한 경로로 흘려보내고, 발생한 어절 검사 요청을 모두 모은다.
    /// (DetectionPipeline.HandleKey의 switch와 같은 동작)
    /// </summary>
    private static List<WordCheck> TypeThrough(IEnumerable<Keystroke> keys)
    {
        var buffer = new WordBuffer();
        var seen = new List<WordCheck>();
        buffer.CheckRequested += wc => seen.Add(wc);

        long now = 1_000;
        foreach (var k in keys)
        {
            now += 50;   // 사람이 치는 정도의 간격 (유휴 리셋에 걸리지 않게)
            var tk = KeyTranslator.Translate(k.Vk, k.Shift);
            switch (tk.Action)
            {
                case KeyAction.Character: buffer.FeedChar(tk.Character, now); break;
                case KeyAction.Backspace: buffer.Backspace(now); break;
                case KeyAction.Boundary: buffer.CommitBoundary(now); break;
                case KeyAction.Reset: buffer.ForceReset(); break;
            }
        }

        // 마지막 어절을 확정시킨다(사용자가 스페이스를 누른 것과 동일).
        buffer.CommitBoundary(now + 50);
        return seen;
    }

    /// <summary>문자열을 타이핑해서 나온 어절들을 돌려준다.</summary>
    private static List<WordCheck> Type(string text)
    {
        KeystrokeSimulator.TryTypeOut(text, out var keys).Should().BeTrue(
            because: $"'{text}'는 두벌식으로 칠 수 있어야 합니다");
        return TypeThrough(keys);
    }

    [Theory]
    [InlineData("안녕하세요")]
    [InlineData("되요")]          // 복합 모음 ㅚ
    [InlineData("갔다")]          // 쌍자음 받침 ㅆ
    [InlineData("괜찮아")]        // 복합 모음 ㅙ + 복합 받침 ㄶ
    [InlineData("읽었다")]        // 복합 받침 ㄺ
    [InlineData("띄어쓰기")]      // 쌍자음 초성 ㄸ + ㅢ
    [InlineData("값어치")]        // 복합 받침 ㅄ
    [InlineData("웬일")]          // 복합 모음 ㅞ 계열
    public void 두벌식으로_친_키는_원래_글자로_조합된다(string text)
    {
        var words = Type(text);

        words.Should().ContainSingle(because: "공백 없는 한 어절이므로 검사 요청도 한 번이어야 합니다");
        words[0].Word.Should().Be(text);
    }

    [Fact]
    public void 공백으로_나뉜_어절은_직전_어절과_함께_전달된다()
    {
        var words = Type("안 되");

        words.Select(w => w.Word).Should().Equal("안", "되");
        words[1].PreviousWord.Should().Be("안", because: "문맥 규칙(an-dwae 등)이 직전 어절을 본다");
    }

    [Fact]
    public void 백스페이스는_마지막_자모를_지운다()
    {
        // "되요" 를 친 뒤 마지막 키(ㅛ)를 지우면 "되ㅇ" 상태가 되고, 한 번 더 지우면 "되".
        KeystrokeSimulator.TryTypeOut("되요", out var keys).Should().BeTrue();
        keys.Add(new Keystroke(0x08, false));   // VK_BACK
        keys.Add(new Keystroke(0x08, false));

        var words = TypeThrough(keys);

        words.Should().ContainSingle();
        words[0].Word.Should().Be("되");
    }

    public static TheoryData<string> RulesWithTypeableExamples()
    {
        var data = new TheoryData<string>();
        foreach (var r in AllRules.Where(r => r.Examples is { Count: > 0 }))
            if (r.Examples!.Any(e => KeystrokeSimulator.TryTypeOut(e, out _)))
                data.Add(r.Id);
        return data;
    }

    /// <summary>
    /// 규칙의 examples를 <b>실제로 타이핑</b>했을 때도 그 규칙이 감지되는지 검증한다.
    /// 규칙을 추가하면 이 게이트가 자동으로 키 입력 경로까지 함께 검증한다.
    /// </summary>
    [Theory]
    [MemberData(nameof(RulesWithTypeableExamples))]
    public void 규칙의_examples는_타이핑해도_감지된다(string ruleId)
    {
        var rule = AllRules.First(r => r.Id == ruleId);
        var engine = AllLevelsEngine();

        foreach (var example in rule.Examples!)
        {
            // 두벌식으로 칠 수 없는 예시(영문·숫자 포함 등)는 이 경로의 대상이 아니다.
            if (!KeystrokeSimulator.TryTypeOut(example, out var keys)) continue;

            var words = TypeThrough(keys);

            bool detected = words.Any(w =>
                engine.Check(w.Word, w.PreviousWord).Any(d => d.Rule.Id == ruleId));

            detected.Should().BeTrue(
                because: $"규칙 '{ruleId}'의 예시 '{example}'을(를) 두벌식으로 타이핑하면 감지돼야 합니다");
        }
    }

    /// <summary>
    /// 이 e2e 게이트가 실제로 상당수의 규칙을 덮고 있는지 확인한다.
    /// 시뮬레이터가 조용히 전부 건너뛰어 "0건 통과"가 되는 상황을 막는다.
    /// </summary>
    [Fact]
    public void 타이핑_게이트는_대부분의_규칙을_덮는다()
    {
        int withExamples = AllRules.Count(r => r.Examples is { Count: > 0 });
        int typeable = RulesWithTypeableExamples().Count;

        withExamples.Should().BeGreaterThan(50, because: "규칙 대부분에 examples가 달려 있어야 합니다");
        typeable.Should().BeGreaterThan((int)(withExamples * 0.9),
            because: "examples가 있는 규칙은 대부분 두벌식으로 칠 수 있어야 합니다");
    }
}
