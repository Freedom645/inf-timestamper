namespace InfTimestamper.Core.Obs;

public sealed record ObsScreenshot(byte[] PngBytes, DateTimeOffset CapturedAt);
