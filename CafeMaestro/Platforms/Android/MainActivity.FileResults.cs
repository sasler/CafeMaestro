using Android.App;
using Android.Content;

namespace CafeMaestro;

public sealed class AndroidActivityResultEventArgs(
    int requestCode,
    Result resultCode,
    Intent? data) : EventArgs
{
    public int RequestCode { get; } = requestCode;

    public Result ResultCode { get; } = resultCode;

    public Intent? Data { get; } = data;
}

public partial class MainActivity
{
    public static event EventHandler<AndroidActivityResultEventArgs>? ActivityResultReceived;

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        ActivityResultReceived?.Invoke(
            this,
            new AndroidActivityResultEventArgs(requestCode, resultCode, data));
    }
}
