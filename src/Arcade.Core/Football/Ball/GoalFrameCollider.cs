using System.Numerics;
using Arcade.Core.Football.Pitch;

namespace Arcade.Core.Football.Ball;

/// <summary>
/// Collisions of the ball with the static world: goal posts (vertical cylinders), crossbars (horizontal
/// cylinders), goal nets (a box open on the pitch side) and the boards around the pitch.
/// Called once per sub-step; sub-steps are short enough that the ball cannot tunnel through anything.
/// </summary>
public static class GoalFrameCollider
{
    public static BallImpacts Resolve(ref BallState state, PitchGeometry pitch, GoalFrameConfig frame, BoardsConfig boards, float radius)
    {
        ArgumentNullException.ThrowIfNull(pitch);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(boards);
        BallImpacts impacts = BallImpacts.None;
        foreach (GoalEnd end in PitchGeometry.Ends)
        {
            impacts |= ResolveGoal(ref state, pitch, frame, radius, end);
        }

        impacts |= ResolveBoards(ref state, pitch, boards, radius);
        return impacts;
    }

    private static BallImpacts ResolveGoal(ref BallState state, PitchGeometry pitch, GoalFrameConfig frame, float radius, GoalEnd end)
    {
        float lineY = pitch.GoalLineY(end);
        float outward = PitchGeometry.Outward(end);
        // Quick rejection: far from this goal.
        float depth = (state.Position.Y - lineY) * outward;
        if (depth < -(radius + pitch.PostRadius + 0.5f) || depth > pitch.GoalDepth + radius + 0.5f)
        {
            return BallImpacts.None;
        }

        BallImpacts impacts = BallImpacts.None;
        float centreZ = state.Position.Z + radius;

        // Posts: vertical cylinders on the goal line.
        if (state.Position.Z < pitch.GoalHeight)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var post = new Vector2(side * (pitch.GoalHalfWidth + pitch.PostRadius), lineY);
                if (PushOutOfCircle(ref state, post, pitch.PostRadius + radius, frame.PostRestitution, frame.PostFriction))
                {
                    impacts |= BallImpacts.Post;
                }
            }
        }

        // Crossbar: horizontal cylinder along X, centred at the top of the goal mouth.
        if (MathF.Abs(state.Position.X) <= pitch.GoalHalfWidth + pitch.PostRadius)
        {
            if (PushOutOfBar(ref state, lineY, pitch.GoalHeight + pitch.PostRadius, pitch.PostRadius + radius, radius, frame))
            {
                impacts |= BallImpacts.Crossbar;
            }
        }

        // Net: box beyond the goal line, open on the pitch side.
        depth = (state.Position.Y - lineY) * outward;
        centreZ = state.Position.Z + radius;
        bool centreInside = depth > 0f && depth < pitch.GoalDepth && MathF.Abs(state.Position.X) < pitch.GoalHalfWidth && centreZ < pitch.GoalHeight;
        if (centreInside)
        {
            impacts |= ConstrainInsideNet(ref state, pitch, frame, radius, lineY, outward);
        }
        else if (depth >= 0f)
        {
            impacts |= PushOutOfNetFromOutside(ref state, pitch, frame, radius, lineY, outward);
        }

        return impacts;
    }

    private static bool PushOutOfCircle(ref BallState state, Vector2 centre, float minDistance, float restitution, float friction)
    {
        Vector2 offset = new Vector2(state.Position.X, state.Position.Y) - centre;
        float distanceSquared = offset.LengthSquared();
        if (distanceSquared >= minDistance * minDistance)
        {
            return false;
        }

        float distance = MathF.Sqrt(distanceSquared);
        Vector2 normal = distance > 1e-5f ? offset / distance : new Vector2(0f, 1f);
        Vector2 corrected = centre + (normal * minDistance);
        state.Position = new Vector3(corrected, state.Position.Z);
        var velocity = new Vector2(state.Velocity.X, state.Velocity.Y);
        float normalSpeed = Vector2.Dot(velocity, normal);
        if (normalSpeed < 0f)
        {
            Vector2 tangent = velocity - (normal * normalSpeed);
            velocity = (tangent * friction) - (normal * normalSpeed * restitution);
            state.Velocity = new Vector3(velocity, state.Velocity.Z);
        }

        return true;
    }

    private static bool PushOutOfBar(ref BallState state, float barY, float barZ, float minDistance, float radius, GoalFrameConfig frame)
    {
        // Work in the (Y, Z) plane with the ball centre.
        var offset = new Vector2(state.Position.Y - barY, state.Position.Z + radius - barZ);
        float distanceSquared = offset.LengthSquared();
        if (distanceSquared >= minDistance * minDistance)
        {
            return false;
        }

        float distance = MathF.Sqrt(distanceSquared);
        Vector2 normal = distance > 1e-5f ? offset / distance : new Vector2(0f, 1f);
        Vector2 corrected = new Vector2(barY, barZ) + (normal * minDistance);
        state.Position = new Vector3(state.Position.X, corrected.X, MathF.Max(0f, corrected.Y - radius));
        var velocity = new Vector2(state.Velocity.Y, state.Velocity.Z);
        float normalSpeed = Vector2.Dot(velocity, normal);
        if (normalSpeed < 0f)
        {
            Vector2 tangent = velocity - (normal * normalSpeed);
            velocity = (tangent * frame.PostFriction) - (normal * normalSpeed * frame.PostRestitution);
            state.Velocity = new Vector3(state.Velocity.X * frame.PostFriction, velocity.X, velocity.Y);
        }

        return true;
    }

    private static BallImpacts ConstrainInsideNet(ref BallState state, PitchGeometry pitch, GoalFrameConfig frame, float radius, float lineY, float outward)
    {
        BallImpacts impacts = BallImpacts.None;
        Vector3 position = state.Position;
        Vector3 velocity = state.Velocity;

        // Back of the net.
        float depth = (position.Y - lineY) * outward;
        float maxDepth = pitch.GoalDepth - radius;
        if (depth > maxDepth)
        {
            position.Y = lineY + (outward * maxDepth);
            if (velocity.Y * outward > 0f)
            {
                velocity.Y = -velocity.Y * frame.NetRestitution;
                velocity.X *= frame.NetFriction;
                velocity.Z *= frame.NetFriction;
            }

            impacts |= BallImpacts.Net;
        }

        // Side netting.
        float maxX = pitch.GoalHalfWidth - radius;
        if (MathF.Abs(position.X) > maxX)
        {
            float sign = MathF.Sign(position.X);
            position.X = sign * maxX;
            if (velocity.X * sign > 0f)
            {
                velocity.X = -velocity.X * frame.NetRestitution;
                velocity.Y *= frame.NetFriction;
                velocity.Z *= frame.NetFriction;
            }

            impacts |= BallImpacts.Net;
        }

        // Roof of the net.
        float maxBottom = pitch.GoalHeight - (2f * radius);
        if (position.Z > maxBottom)
        {
            position.Z = MathF.Max(0f, maxBottom);
            if (velocity.Z > 0f)
            {
                velocity.Z = -velocity.Z * frame.NetRestitution;
                velocity.X *= frame.NetFriction;
                velocity.Y *= frame.NetFriction;
            }

            impacts |= BallImpacts.Net;
        }

        state.Position = position;
        state.Velocity = velocity;
        return impacts;
    }

    private static BallImpacts PushOutOfNetFromOutside(ref BallState state, PitchGeometry pitch, GoalFrameConfig frame, float radius, float lineY, float outward)
    {
        // Closest point of the goal box to the ball centre, in local coordinates (x, depth, z).
        float depth = (state.Position.Y - lineY) * outward;
        float centreZ = state.Position.Z + radius;
        var centre = new Vector3(state.Position.X, depth, centreZ);
        var closest = new Vector3(
            Math.Clamp(centre.X, -pitch.GoalHalfWidth, pitch.GoalHalfWidth),
            Math.Clamp(centre.Y, 0f, pitch.GoalDepth),
            Math.Clamp(centre.Z, 0f, pitch.GoalHeight));
        Vector3 offset = centre - closest;
        float distanceSquared = offset.LengthSquared();
        if (distanceSquared >= radius * radius || distanceSquared < 1e-10f)
        {
            return BallImpacts.None;
        }

        float distance = MathF.Sqrt(distanceSquared);
        Vector3 normal = offset / distance;
        Vector3 corrected = closest + (normal * radius);
        state.Position = new Vector3(corrected.X, lineY + (outward * corrected.Y), MathF.Max(0f, corrected.Z - radius));
        var localVelocity = new Vector3(state.Velocity.X, state.Velocity.Y * outward, state.Velocity.Z);
        float normalSpeed = Vector3.Dot(localVelocity, normal);
        if (normalSpeed < 0f)
        {
            localVelocity -= normal * (normalSpeed * (1f + frame.OuterNetRestitution));
            localVelocity *= frame.NetFriction;
            state.Velocity = new Vector3(localVelocity.X, localVelocity.Y * outward, localVelocity.Z);
        }

        return BallImpacts.Net;
    }

    private static BallImpacts ResolveBoards(ref BallState state, PitchGeometry pitch, BoardsConfig boards, float radius)
    {
        BallImpacts impacts = BallImpacts.None;
        float maxX = pitch.BoardsHalfWidth - radius;
        float maxY = pitch.BoardsHalfLength - radius;
        if (MathF.Abs(state.Position.X) > maxX)
        {
            float sign = MathF.Sign(state.Position.X);
            state.Position.X = sign * maxX;
            if (state.Velocity.X * sign > 0f)
            {
                state.Velocity.X = -state.Velocity.X * boards.Restitution;
                state.Velocity.Y *= boards.Friction;
            }

            impacts |= BallImpacts.Board;
        }

        if (MathF.Abs(state.Position.Y) > maxY)
        {
            float sign = MathF.Sign(state.Position.Y);
            state.Position.Y = sign * maxY;
            if (state.Velocity.Y * sign > 0f)
            {
                state.Velocity.Y = -state.Velocity.Y * boards.Restitution;
                state.Velocity.X *= boards.Friction;
            }

            impacts |= BallImpacts.Board;
        }

        return impacts;
    }
}
