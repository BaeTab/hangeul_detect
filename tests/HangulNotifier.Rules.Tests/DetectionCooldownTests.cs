using FluentAssertions;
using HangulNotifier.Core.Rules;

namespace HangulNotifier.Rules.Tests;

public class DetectionCooldownTests
{
    [Fact]
    public void 첫_알림_허용하고_5초내_중복은_차단()
    {
        var cd = new DetectionCooldown();
        cd.ShouldNotify("되요", "doeyo", 1000).Should().BeTrue();
        cd.ShouldNotify("되요", "doeyo", 3000).Should().BeFalse();   // 2초 후 중복
        cd.ShouldNotify("되요", "doeyo", 6001).Should().BeTrue();    // 5초 경과 → 재허용
    }

    [Fact]
    public void 다른_규칙이나_어절은_독립적으로_허용()
    {
        var cd = new DetectionCooldown();
        cd.ShouldNotify("되요", "doeyo", 1000).Should().BeTrue();
        cd.ShouldNotify("되요", "other", 1000).Should().BeTrue();    // 다른 규칙
        cd.ShouldNotify("다른", "doeyo", 1000).Should().BeTrue();    // 다른 어절
    }
}
