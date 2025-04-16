namespace CafeMaestro;

public class RotateBehavior : Behavior<Image>
{
    private bool _isAttached;
    private Image? _image;
    private double _currentRotation = 0;
    private const double RotationIncrement = 6;
    private const int AnimationInterval = 30; // milliseconds
    
    protected override void OnAttachedTo(Image bindable)
    {
        base.OnAttachedTo(bindable);
        
        _image = bindable;
        _isAttached = true;
        
        // Start animation
        StartRotationAnimation();
    }
    
    protected override void OnDetachingFrom(Image bindable)
    {
        _isAttached = false;
        _image = null;
        
        base.OnDetachingFrom(bindable);
    }
    
    private async void StartRotationAnimation()
    {
        while (_isAttached && _image != null)
        {
            // Update rotation
            _currentRotation = (_currentRotation + RotationIncrement) % 360;
            _image.Rotation = _currentRotation;
            
            // Wait for next frame
            await Task.Delay(AnimationInterval);
        }
    }
}