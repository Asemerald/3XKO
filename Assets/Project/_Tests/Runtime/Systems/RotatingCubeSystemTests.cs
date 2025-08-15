using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

// Note: Tests exercise the pure Execute(ref LocalTransform, in RotateSpeed) method of RotatingCubeSystem.RotatingCubeJob.
// This avoids spinning up a World or scheduling jobs, making tests fast and deterministic.
// Library/Framework: Unity Test Framework (NUnit)

public class RotatingCubeSystemTests
{
    // Helper to create a LocalTransform with identity rotation, zero position, and scale 1.
    private static LocalTransform MakeDefaultTransform()
    {
        // LocalTransform API in Entities typically uses static factory FromPositionRotationScale.
        // Fall back to constructing and setting fields if needed, but prefer the factory for clarity.
        var t = LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f);
        return t;
    }

    // Compares quaternions with a tolerance since floating point and angle wrapping can produce very close but not exact values.
    private static void AssertQuatApproximately(quaternion expected, quaternion actual, float tolerance = 1e-5f)
    {
        // Normalize both to be safe
        expected = math.normalize(expected);
        actual = math.normalize(actual);

        // Quaternions q and -q represent the same rotation; account for that by taking min of distances
        float4 e = expected.value;
        float4 a = actual.value;

        float diffA = math.length(a - e);
        float diffB = math.length(a + e); // account for sign flip
        float diff = math.min(diffA, diffB);

        Assert.LessOrEqual(diff, tolerance, $"Quaternion difference {diff} exceeded tolerance {tolerance}. Expected {expected}, Actual {actual}");
    }

    [Test]
    public void Execute_ZeroSpeed_KeepsTransformUnchanged()
    {
        var job = new RotatingCubeSystem.RotatingCubeJob { DeltaTime = 1.0f };
        var lt = MakeDefaultTransform();
        var original = lt;
        var speed = new RotateSpeed { Value = 0f };

        job.Execute(ref lt, in speed);

        AssertQuatApproximately(original.Rotation, lt.Rotation);
        Assert.That(lt.Position, Is.EqualTo(original.Position));
        Assert.That(lt.Scale, Is.EqualTo(original.Scale));
    }

    [Test]
    public void Execute_PositiveSpeed_RotatesAroundYBySpeedTimesDeltaTime()
    {
        float speedRadPerSec = math.radians(90f); // 90 deg/sec
        float dt = 1.0f; // 1 second -> 90 degrees
        var job = new RotatingCubeSystem.RotatingCubeJob { DeltaTime = dt };

        var lt = MakeDefaultTransform();
        var speed = new RotateSpeed { Value = speedRadPerSec };

        job.Execute(ref lt, in speed);

        // Expected rotation is identity rotated by +90 degrees around Y
        var expectedRot = quaternion.AxisAngle(math.up(), speedRadPerSec * dt);

        AssertQuatApproximately(expectedRot, lt.Rotation);
        Assert.That(lt.Position, Is.EqualTo(float3.zero));
        Assert.That(lt.Scale, Is.EqualTo(1f));
    }

    [Test]
    public void Execute_NegativeSpeed_RotatesAroundYInOppositeDirection()
    {
        float speedRadPerSec = -math.radians(45f); // -45 deg/sec
        float dt = 2.0f; // 2 seconds -> -90 degrees total
        var job = new RotatingCubeSystem.RotatingCubeJob { DeltaTime = dt };

        var lt = MakeDefaultTransform();
        var speed = new RotateSpeed { Value = speedRadPerSec };

        job.Execute(ref lt, in speed);

        var expectedRot = quaternion.AxisAngle(math.up(), speedRadPerSec * dt);

        AssertQuatApproximately(expectedRot, lt.Rotation);
        Assert.That(lt.Position, Is.EqualTo(float3.zero));
        Assert.That(lt.Scale, Is.EqualTo(1f));
    }

    [Test]
    public void Execute_MultipleUpdates_AccumulatesRotation()
    {
        var job = new RotatingCubeSystem.RotatingCubeJob { DeltaTime = 0.5f }; // half-second steps
        float speedRadPerSec = math.radians(60f); // 60 deg/sec
        var speed = new RotateSpeed { Value = speedRadPerSec };

        var lt = MakeDefaultTransform();

        // Two updates at 0.5s each -> 1.0s total -> 60 degrees
        job.Execute(ref lt, in speed);
        job.Execute(ref lt, in speed);

        var expectedRot = quaternion.AxisAngle(math.up(), speedRadPerSec * (0.5f + 0.5f));

        AssertQuatApproximately(expectedRot, lt.Rotation);
        Assert.That(lt.Position, Is.EqualTo(float3.zero));
        Assert.That(lt.Scale, Is.EqualTo(1f));
    }

    [Test]
    public void Execute_LargeAngle_WrapsButEquivalentRotation()
    {
        var job = new RotatingCubeSystem.RotatingCubeJob { DeltaTime = 1.0f };

        // 360 degrees + 45 degrees -> effectively 45 degrees
        float speedRadPerSec = math.radians(405f); // 360 + 45
        var speed = new RotateSpeed { Value = speedRadPerSec };
        var lt = MakeDefaultTransform();

        job.Execute(ref lt, in speed);

        var expectedRot = quaternion.AxisAngle(math.up(), math.radians(45f));

        AssertQuatApproximately(expectedRot, lt.Rotation);
    }

    [Test]
    public void Execute_WithPreexistingRotation_AppliesRelativeYRotation()
    {
        var job = new RotatingCubeSystem.RotatingCubeJob { DeltaTime = 1.0f };

        var lt = LocalTransform.FromPositionRotationScale(
            new float3(1f, 2f, 3f),
            quaternion.AxisAngle(math.right(), math.radians(30f)), // initial X rotation
            2f);

        var speed = new RotateSpeed { Value = math.radians(90f) };

        job.Execute(ref lt, in speed);

        // Expected: initial rotation then a Y rotation relative to current transform
        var expected = LocalTransform.FromPositionRotationScale(
            new float3(1f, 2f, 3f),
            math.mul(quaternion.AxisAngle(math.up(), math.radians(90f)), quaternion.AxisAngle(math.right(), math.radians(30f))),
            2f);

        // Position & Scale preserved
        Assert.That(lt.Position, Is.EqualTo(expected.Position));
        Assert.That(lt.Scale, Is.EqualTo(expected.Scale));

        // Rotation approximately equal; since LocalTransform.RotateY applies a relative Y rotation,
        // the multiplication order is important (Y then existing rotation).
        // LocalTransform.RotateY applies the new rotation relative to the current, which is equivalent to:
        // lt.Rotation = mul(quaternion.RotateY(angle), lt.Rotation)
        AssertQuatApproximately(expected.Rotation, lt.Rotation);
    }

    [Test]
    public void Execute_NaNSpeed_ProducesNaNRotation()
    {
        var job = new RotatingCubeSystem.RotatingCubeJob { DeltaTime = 1.0f };
        var lt = MakeDefaultTransform();
        var speed = new RotateSpeed { Value = float.NaN };

        job.Execute(ref lt, in speed);

        // Any NaN in angle will propagate into quaternion components
        var v = lt.Rotation.value;
        Assert.IsTrue(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsNaN(v.w),
            "Expected NaN in rotation when speed is NaN.");
    }

    [Test]
    public void Execute_ZeroDeltaTime_NoChangeRegardlessOfSpeed()
    {
        var job = new RotatingCubeSystem.RotatingCubeJob { DeltaTime = 0f };
        var lt = MakeDefaultTransform();
        var original = lt;
        var speed = new RotateSpeed { Value = math.radians(123f) };

        job.Execute(ref lt, in speed);

        AssertQuatApproximately(original.Rotation, lt.Rotation);
        Assert.That(lt.Position, Is.EqualTo(original.Position));
        Assert.That(lt.Scale, Is.EqualTo(original.Scale));
    }
}