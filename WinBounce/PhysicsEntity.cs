namespace WinBounce;

public class PhysicsEntity
{
    public string Id { get; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double VelocityX { get; set; }
    public double VelocityY { get; set; }

    public double Mass { get; set; } = 0.1;
    // public double Mass => Density * Volume;
    // public double Volume => Width * Height;
    // public double Density { get; set; } = 0.000001;
    public bool Held { get; set; }
    public double Left => X;
    public double Right => X + Width;
    public double Top => Y + Height;
    public double Bottom => Y;
    public PhysicsEntity(string id, double x, double y, double width, double height)
    {
        Id = id;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public (double x, double y, double width, double height) GetCoord() => (X, Y, Width, Height);
    public double CenterX => Left + Right / 2;
    public double CenterY => Top + Bottom / 2;

    public bool Intersect(PhysicsEntity entity)
    {
        var xOverlapped = IntersectX(entity);
        var yOverlapped = IntersectY(entity);
        return xOverlapped && yOverlapped;
    }
    
    public bool IntersectX(PhysicsEntity entity)
    {
        return Left < entity.Right && entity.Left < Right;
    }

    public bool IntersectY(PhysicsEntity entity)
    {
        return Bottom < entity.Top && entity.Bottom < Top;
    }
}
