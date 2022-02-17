using System;
using System.Collections.Generic;
using System.Linq;

namespace WinBounce;

/// <summary>
/// World origin (0, 0) is bottom left
/// </summary>
public class PhysicsWorld
{
    private readonly double _width;
    private readonly double _height;
    private readonly List<PhysicsEntity> _entities = new ();
    private readonly double _gravity = -10;
    // private readonly double _gravity = 0;
    private readonly double _maxVelocity = 100;

    public PhysicsWorld(double width, double height)
    {
        _width = width;
        _height = height;
    }

    public void AddEntity(string id, double x, double y, double width, double height)
    {
        var entity = new PhysicsEntity(id, x, y, width, height);
        _entities.Add(entity);
    }

    private PhysicsEntity? GetEntity(string id)
    {
        return _entities.FirstOrDefault(e => e.Id == id);
    }
    
    public (double x, double y, double width, double height)? GetEntityCoord(string id)
    {
        return GetEntity(id)?.GetCoord();
    }

    public (double x, double y, double width, double height) GetCoordTranslated((double x, double y, double width, double height) coord)
    {
        return (coord.x, _height - coord.y, coord.width, coord.height);
    }
    
    public (double x, double y, double width, double height)? GetEntityCoordTranslated(string id)
    {
        if (GetEntity(id)?.GetCoord() is not { } coord) return null;
        return GetCoordTranslated(coord);
    }

    public void Update()
    {
        foreach (var entity in _entities.Where(e => !e.Held))
        {
            var accelY = _gravity;
            var minY = entity.Height;
            var minX = 0;
            var maxY = _height;
            var maxX = _width - entity.Width;
            
            // air drag
            entity.VelocityY *= 0.9995;
            entity.VelocityX *= 0.9995;
            
            // gravity
            entity.VelocityY += accelY * entity.Mass;
            entity.VelocityY = Math.Clamp(entity.VelocityY, -1 * _maxVelocity, _maxVelocity);

            // collide box
            var collideEntities = _entities.Where(e => e.Id != entity.Id && e.Intersect(entity)).ToList();
            var collideEntitiesY = collideEntities.Where(e => e.IntersectPrimaryAxis(entity).isY).ToList();
            var collideEntitiesX = collideEntities.Where(e => e.IntersectPrimaryAxis(entity).isX).ToList();
            
            entity.Y += entity.VelocityY;
            foreach (var collideEntity in collideEntitiesY)
            {
                var sign = collideEntity.CenterY > entity.CenterY ? 1 : -1;
                collideEntity.VelocityY += sign * Math.Abs(collideEntitiesY.Sum(e => e.VelocityY) + entity.VelocityY) / (collideEntitiesY.Count + 1);
            }
            // collide floor
            if (entity.Y >= maxY || entity.Y <= minY || collideEntitiesY.Any())
            {
                entity.VelocityY *= -0.4;
                // friction
                entity.VelocityX *= 0.3;
            }

            // collide box
            entity.X += entity.VelocityX;
            foreach (var collideEntity in collideEntitiesX)
            {
                var sign = (collideEntity.CenterX > entity.CenterX ? 1 : -1);
                collideEntity.VelocityX += sign * Math.Abs(collideEntitiesX.Sum(e => e.VelocityX) + entity.VelocityX) / (collideEntitiesX.Count + 1);
            }
            // collide wall
            if (entity.X >= maxX || entity.X <= minX || collideEntitiesX.Any())
            {
                entity.VelocityX *= -0.4;
                // friction
                entity.VelocityY *= 0.3;
            }

            // for each collider, 
            var collideMaxValueOnLeft = collideEntitiesX
                .Where(e => e.CenterX < entity.CenterX)
                .Select(e => e.Right)
                .DefaultIfEmpty(minX)
                .Max();
            var collideMinValueOnRight = collideEntitiesX
                .Where(e => e.CenterX > entity.CenterX)
                .Select(e => e.Left - entity.Width)
                .DefaultIfEmpty(maxX)
                .Min();
            var minComputedX = Math.Max(minX, collideMaxValueOnLeft);
            var maxComputedX = Math.Min(maxX, collideMinValueOnRight);
            entity.X = maxComputedX > minComputedX
                ? Math.Clamp(entity.X, minComputedX, maxComputedX)
                : minComputedX;
            var collideMaxValueOnBottom = collideEntitiesY
                .Where(e => e.CenterY < entity.CenterY)
                .Select(e => e.Top + entity.Height)
                .DefaultIfEmpty(minY)
                .Max();
            var collideMinValueOnTop = collideEntitiesY
                .Where(e => e.CenterY > entity.CenterY)
                .Select(e => e.Bottom)
                .DefaultIfEmpty(maxY)
                .Min();
            var minComputedY = Math.Max(minY, collideMaxValueOnBottom);
            var maxComputedY = Math.Min(maxY, collideMinValueOnTop);
            entity.Y = maxComputedY > minComputedY
                ? Math.Clamp(entity.Y, minComputedY, maxComputedY)
                : minComputedY;
        }
    }

    public void UpdateEntityCoord(string id, double x, double y, double width, double height)
    {
        var entity = GetEntity(id);
        if (entity == null) return;
        
        // compute acceleration
        var newVelocityX = x - entity.X;
        entity.VelocityX = PhysicsUtils.Lerp(entity.VelocityX, newVelocityX, 0.5);
        var newVelocityY = y - entity.Y;
        entity.VelocityY = PhysicsUtils.Lerp(entity.VelocityY, newVelocityY, 0.5);
        
        entity.X = x;
        entity.Y = y;
        entity.Width = width;
        entity.Height = height;
    }

    public void UpdateEntityVelocity(string id, int velocity)
    {
        var entity = GetEntity(id);
        if (entity == null) return;
        entity.VelocityY = velocity;
    }

    public void UpdateEntityHeld(string id, bool held)
    {
        var entity = GetEntity(id);
        if (entity == null) return;
        entity.Held = held;
    }

    public void RemoveEntity(string id)
    {
        var entity = _entities.FirstOrDefault(e => e.Id == id);
        if (entity == null) return;
        _entities.Remove(entity);
    }
}