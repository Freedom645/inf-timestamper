using InfTimestamper.ViewModels;

namespace InfTimestamper.Core.Tests.ViewModels;

public class DateTimeEditDialogViewModelTests
{
    private static readonly DateTimeOffset BaseTime
        = new(2026, 5, 17, 18, 0, 0, TimeSpan.FromHours(9));

    [Fact]
    public void SingleValue_InitialDisplaysFormattedText()
    {
        var vm = new DateTimeEditDialogViewModel(new[] { BaseTime });
        Assert.False(vm.IsMultiple);
        Assert.Equal("2026-05-17 18:00:00", vm.EditText);
        Assert.True(vm.IsTextValid);
    }

    [Fact]
    public void SingleValue_DirectEdit_ProducesNewDateTimeOffset()
    {
        var vm = new DateTimeEditDialogViewModel(new[] { BaseTime });
        vm.EditText = "2026-05-17 18:30:15";

        Assert.True(vm.IsTextValid);
        Assert.True(vm.ConfirmCommand.CanExecute(null));

        vm.ConfirmCommand.Execute(null);
        Assert.True(vm.DialogResult);

        Assert.NotNull(vm.Result);
        Assert.Single(vm.Result!);
        var newValue = vm.Result![0];
        Assert.Equal(new DateTime(2026, 5, 17, 18, 30, 15), newValue.LocalDateTime);
        Assert.Equal(TimeSpan.FromHours(9), newValue.Offset);
    }

    [Fact]
    public void InvalidText_DisablesConfirm()
    {
        var vm = new DateTimeEditDialogViewModel(new[] { BaseTime });
        vm.EditText = "not a date";

        Assert.False(vm.IsTextValid);
        Assert.False(vm.ConfirmCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("-00:01:00", 17, 59, 0)]
    [InlineData("-00:00:10", 17, 59, 50)]
    [InlineData("-00:00:01", 17, 59, 59)]
    [InlineData("00:00:01",  18, 0, 1)]
    [InlineData("00:00:10",  18, 0, 10)]
    [InlineData("00:01:00",  18, 1, 0)]
    public void ShiftCommand_AppliesRelativeOffset(string param, int h, int m, int s)
    {
        var vm = new DateTimeEditDialogViewModel(new[] { BaseTime });
        vm.ShiftCommand.Execute(param);
        Assert.Equal($"2026-05-17 {h:D2}:{m:D2}:{s:D2}", vm.EditText);
    }

    [Fact]
    public void MultipleValues_ShowsMinimumOnly()
    {
        var values = new[]
        {
            BaseTime.AddSeconds(30),
            BaseTime,
            BaseTime.AddMinutes(2),
        };
        var vm = new DateTimeEditDialogViewModel(values);

        Assert.True(vm.IsMultiple);
        Assert.Equal(3, vm.EditingCount);
        Assert.Equal("2026-05-17 18:00:00", vm.EditText);
    }

    [Fact]
    public void MultipleValues_ShiftPlusOneMinute_ConfirmPreservesRelativeSpacing()
    {
        var values = new[]
        {
            BaseTime,
            BaseTime.AddSeconds(30),
            BaseTime.AddMinutes(2),
        };
        var vm = new DateTimeEditDialogViewModel(values);

        vm.ShiftCommand.Execute("00:01:00");
        vm.ConfirmCommand.Execute(null);

        Assert.NotNull(vm.Result);
        Assert.Equal(3, vm.Result!.Count);
        Assert.Equal(BaseTime.AddMinutes(1), vm.Result[0]);
        Assert.Equal(BaseTime.AddSeconds(90), vm.Result[1]);
        Assert.Equal(BaseTime.AddMinutes(3), vm.Result[2]);
    }

    [Fact]
    public void CancelCommand_LeavesResultNull()
    {
        var vm = new DateTimeEditDialogViewModel(new[] { BaseTime });
        vm.EditText = "2026-05-17 19:00:00";
        vm.CancelCommand.Execute(null);

        Assert.False(vm.DialogResult);
        Assert.Null(vm.Result);
    }

    [Fact]
    public void RequestClose_FiresOnConfirmAndCancel()
    {
        var vm = new DateTimeEditDialogViewModel(new[] { BaseTime });
        int closeFires = 0;
        vm.RequestClose += () => closeFires++;

        vm.ConfirmCommand.Execute(null);
        Assert.Equal(1, closeFires);

        var vm2 = new DateTimeEditDialogViewModel(new[] { BaseTime });
        int closeFires2 = 0;
        vm2.RequestClose += () => closeFires2++;
        vm2.CancelCommand.Execute(null);
        Assert.Equal(1, closeFires2);
    }

    [Fact]
    public void ShiftCommand_NonTimeSpanParameter_Ignored()
    {
        var vm = new DateTimeEditDialogViewModel(new[] { BaseTime });
        var original = vm.EditText;
        vm.ShiftCommand.Execute("not a time");
        Assert.Equal(original, vm.EditText);
    }
}
