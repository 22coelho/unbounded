using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unbounded.App;
using Unbounded.Track;
using Unbounded.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unbounded.Tests.PlayMode
{
    /// <summary>
    /// Drives the generated circuit in the real Main scene. Mesh generation,
    /// rendering, and the chase camera are verified here rather than in
    /// EditMode, because they only exist once the scene is playing.
    /// </summary>
    public sealed class MainSceneSmokeTests
    {
        private static IEnumerator LoadMainScene()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
        }

        private static void DisablePlayerInput()
        {
            foreach (PlayerInputReader reader in Object.FindObjectsByType<PlayerInputReader>())
            {
                reader.enabled = false;
            }
        }

        /// <summary>
        /// Moves a Rigidbody, not just its Transform. Physics only copies a
        /// Transform into the simulation when <c>Physics.autoSyncTransforms</c>
        /// is on, so a bare <c>transform.position</c> write on a body is
        /// silently undone on the next step.
        /// </summary>
        private static void Teleport(Rigidbody body, Vector3 position, Quaternion rotation)
        {
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private static TrackRoot FindTrack()
        {
            TrackRoot track = Object.FindAnyObjectByType<TrackRoot>();
            Assert.That(track, Is.Not.Null, "The Main scene must contain a generated Track.");
            Assert.That(track.Path, Is.Not.Null, "The Track must have a built TrackPath.");
            return track;
        }

        [UnityTest]
        public IEnumerator RoadKerbAndBarrierRunUnbrokenAroundTheWholeCircuit()
        {
            yield return LoadMainScene();

            TrackRoot track = FindTrack();
            TrackPath path = track.Path;
            TrackShape shape = track.Shape;

            Transform road = Generated(track, "Road");
            Transform kerbs = Generated(track, "Kerbs");
            Transform barriers = Generated(track, "Barriers");

            Assert.That(path.Length, Is.GreaterThan(1000f), "The circuit should be a full lap, not a stub.");

            // No gaps or overlaps: half-metre steps along the Centreline never
            // jump further than their own spacing, including across the seam.
            float previous = path.Length - 0.5f;
            for (float distance = 0f; distance < path.Length; distance += 0.5f)
            {
                Vector3 point = track.transform.TransformPoint(path.SamplePosition(distance));
                Vector3 last = track.transform.TransformPoint(path.SamplePosition(previous));
                Assert.That(Vector3.Distance(point, last), Is.LessThan(1f),
                    "The Centreline jumps at " + distance.ToString("0.0") + " metres.");
                previous = distance;
            }

            // Probe at half the mesh's own ring spacing, so a missing ring
            // cannot slip between two samples undetected.
            int checkedPoints = 0;
            for (float distance = 0f; distance < path.Length; distance += shape.MeshSpacing * 0.5f)
            {
                Pose pose = track.SamplePose(distance);
                Vector3 right = pose.rotation * Vector3.right;
                Vector3 overhead = pose.position + Vector3.up * 3f;

                AssertRoad(road, overhead, "road at " + distance.ToString("0.0") + " m");
                AssertKerb(kerbs, overhead + right * (shape.HalfWidth + shape.KerbWidth * 0.5f),
                    "right kerb at " + distance.ToString("0.0") + " m");
                AssertKerb(kerbs, overhead - right * (shape.HalfWidth + shape.KerbWidth * 0.5f),
                    "left kerb at " + distance.ToString("0.0") + " m");
                AssertBarrier(barriers, pose.position, right, shape.BarrierOffset,
                    "right barrier at " + distance.ToString("0.0") + " m");
                AssertBarrier(barriers, pose.position, -right, shape.BarrierOffset,
                    "left barrier at " + distance.ToString("0.0") + " m");

                checkedPoints++;
            }

            Assert.That(checkedPoints, Is.GreaterThan(400), "The sweep should cover the whole circuit.");
        }

        /// <summary>The generated child mesh, which must exist or the scene is broken.</summary>
        private static Transform Generated(TrackRoot track, string name)
        {
            Transform part = track.transform.Find("Generated/" + name);
            Assert.That(part, Is.Not.Null, "The Track did not generate a " + name + " mesh.");
            return part;
        }

        private static void AssertRoad(Transform road, Vector3 origin, string label)
        {
            Assert.That(TryRaycast(origin, Vector3.down, out RaycastHit hit),
                Is.True, "No collider under the " + label + ".");
            Assert.That(hit.collider.transform, Is.EqualTo(road),
                "The " + label + " is not the generated Road (hit " + hit.collider.name + ").");
            Assert.That(hit.point.y, Is.InRange(0.01f, 0.06f),
                "The " + label + " is not where the Track says it is (y = " + hit.point.y + ").");
        }

        private static void AssertKerb(Transform kerbs, Vector3 origin, string label)
        {
            Assert.That(TryRaycast(origin, Vector3.down, out RaycastHit hit),
                Is.True, "No collider under the " + label + ".");
            Assert.That(hit.collider.transform, Is.EqualTo(kerbs),
                "The " + label + " is not the generated Kerbs (hit " + hit.collider.name + ").");
            Assert.That(hit.point.y, Is.InRange(0.02f, 0.13f),
                "The " + label + " is missing or flat (y = " + hit.point.y + ").");
        }

        private static void AssertBarrier(
            Transform barriers, Vector3 centre, Vector3 right, float offset, string label)
        {
            var origin = centre + Vector3.up * 0.5f;
            Assert.That(TryRaycast(origin, right, out RaycastHit hit, offset + 2f),
                Is.True, "The " + label + " is missing.");
            Assert.That(hit.collider.transform, Is.EqualTo(barriers),
                "The " + label + " is not the generated Barriers (hit " + hit.collider.name + ").");
            Assert.That(hit.distance, Is.EqualTo(offset).Within(0.6f),
                "The " + label + " is not where the Track shape puts it (" + hit.distance + " m).");
        }

        /// <summary>
        /// Raycasts in a direction, ignoring the car and any other dynamic body,
        /// and returns the closest static hit.
        /// </summary>
        private static bool TryRaycast(Vector3 origin, Vector3 direction, out RaycastHit hit,
            float maxDistance = 6f)
        {
            bool found = false;
            hit = default;
            foreach (RaycastHit candidate in Physics.RaycastAll(origin, direction, maxDistance,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (candidate.collider.attachedRigidbody != null)
                {
                    continue;
                }

                if (found && candidate.distance >= hit.distance)
                {
                    continue;
                }

                hit = candidate;
                found = true;
            }

            return found;
        }

        [UnityTest]
        public IEnumerator CarSpawnsOnTheStartLineFacingAlongTheTrack()
        {
            yield return LoadMainScene();

            TrackRoot track = FindTrack();
            CarController car = Object.FindAnyObjectByType<CarController>();
            Assert.That(car, Is.Not.Null, "The Main scene must contain a drivable car.");

            Pose start = track.SamplePose(0f);
            Vector3 offset = car.transform.position - start.position;
            offset.y = 0f;
            Assert.That(offset.magnitude, Is.LessThan(3f),
                "The car should spawn on the start/finish line.");

            Assert.That(Vector3.Dot(car.transform.forward, start.rotation * Vector3.forward), Is.GreaterThan(0.98f),
                "The car should face along the Track at the start line.");
            Assert.That(track.SurfaceAt(car.transform.position), Is.EqualTo(TrackSurface.Road),
                "The car should spawn on the road, not on the grass.");
        }

        [UnityTest]
        public IEnumerator CarDrivesAndSteersWithTheChaseCameraBehind()
        {
            var errors = new List<string>();
            Application.LogCallback collectErrors = (condition, stackTrace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    errors.Add(condition);
                }
            };

            Application.logMessageReceived += collectErrors;
            try
            {
                yield return LoadMainScene();

                CarController car = Object.FindAnyObjectByType<CarController>();
                Assert.That(car, Is.Not.Null, "The Main scene must contain a drivable car.");
                TrackRoot track = FindTrack();
                DisablePlayerInput();

                yield return new WaitForFixedUpdate();
                Vector3 start = car.transform.position;

                car.SetInput(new HandlingInput { Throttle = 1f });

                for (int i = 0; i < 120; i++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float distance = Vector3.Distance(start, car.transform.position);
                Assert.That(distance, Is.GreaterThan(15f), "Throttle should move the car down the circuit.");

                float lateral = track.Project(car.transform.position).LateralOffset;
                Assert.That(Mathf.Abs(lateral), Is.LessThan(track.Shape.HalfWidth),
                    "Driving straight off the line should keep the car on the road.");
                Assert.That(track.SurfaceAt(car.transform.position), Is.EqualTo(TrackSurface.Road));

                Camera camera = Camera.main;
                Assert.That(camera, Is.Not.Null);
                Vector3 cameraOffset = camera.transform.position - car.transform.position;
                Assert.That(Vector3.Dot(cameraOffset, car.transform.forward), Is.LessThan(0f),
                    "The chase camera should sit behind the car.");
                Assert.That(cameraOffset.magnitude, Is.LessThan(12f),
                    "The chase camera should stay close enough to read the road ahead.");

                car.SetInput(new HandlingInput { Throttle = 0.4f, Steer = 1f });
                for (int i = 0; i < 40; i++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, car.transform.eulerAngles.y)), Is.GreaterThan(5f),
                    "Steering while moving should turn the car.");
            }
            finally
            {
                Application.logMessageReceived -= collectErrors;
            }

            Assert.That(errors, Is.Empty, "The scene logged errors: " + string.Join(" | ", errors.Take(5)));
        }

        [UnityTest]
        public IEnumerator BarrierStopsTheCarWithoutSwallowingOrTrappingIt()
        {
            yield return LoadMainScene();

            TrackRoot track = FindTrack();
            TrackShape shape = track.Shape;
            CarController car = Object.FindAnyObjectByType<CarController>();
            Assert.That(car, Is.Not.Null, "The Main scene must contain a drivable car.");
            DisablePlayerInput();

            // Park on the road, squarely facing the right-hand Barrier.
            Pose pose = track.SamplePose(60f);
            Vector3 right = pose.rotation * Vector3.right;
            Rigidbody body = car.GetComponent<Rigidbody>();
            Teleport(body,
                pose.position + right * 5f + Vector3.up * 0.1f,
                Quaternion.LookRotation(right, Vector3.up));
            car.SetInput(default);
            yield return new WaitForFixedUpdate();

            TrackProjection start = track.Project(car.transform.position);
            Assert.That(start.LateralOffset, Is.EqualTo(5f).Within(0.2f),
                "The test failed to place the car on the road (lateral " + start.LateralOffset + " m).");

            // The nose of the car reaches the Barrier face when the car's
            // centre is half a car length short of it.
            float noseReach = car.GetComponent<BoxCollider>().size.z * 0.5f;

            car.SetInput(new HandlingInput { Throttle = 1f });
            for (int i = 0; i < 300; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            float lateral = track.Project(car.transform.position).LateralOffset;
            float nose = lateral + noseReach;

            Assert.That(nose, Is.GreaterThan(shape.BarrierOffset - 0.1f),
                "The car never reached the Barrier (nose at " + nose + " m).");
            Assert.That(nose, Is.LessThan(shape.BarrierOffset + shape.BarrierThickness),
                "The car pushed through the Barrier (nose at " + nose + " m).");
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(1f),
                "The Barrier should hold the car still while the throttle is down (speed " +
                body.linearVelocity.magnitude + " m/s).");
            Assert.That(car.transform.position.y, Is.GreaterThan(0f),
                "The Barrier should not swallow the car.");

            car.SetInput(new HandlingInput { Brake = 1f });
            Vector3 held = car.transform.position;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(Vector3.Distance(held, car.transform.position), Is.GreaterThan(2f),
                "The car should be able to reverse away from the Barrier instead of being trapped.");
        }

        [UnityTest]
        public IEnumerator ChaseCameraDoesNotPassThroughBarriers()
        {
            yield return LoadMainScene();

            TrackRoot track = FindTrack();
            TrackShape shape = track.Shape;
            CarController car = Object.FindAnyObjectByType<CarController>();
            Assert.That(car, Is.Not.Null, "The Main scene must contain a drivable car.");
            DisablePlayerInput();

            // Park on the road facing across it, so the natural chase
            // position (7.5 m behind the car) is beyond the right Barrier.
            Pose pose = track.SamplePose(90f);
            Vector3 right = pose.rotation * Vector3.right;
            Rigidbody body = car.GetComponent<Rigidbody>();
            Teleport(body,
                pose.position + right * (shape.HalfWidth - 1.5f) + Vector3.up * 0.1f,
                Quaternion.AngleAxis(-90f, Vector3.up) * pose.rotation);
            car.SetInput(default);
            yield return new WaitForFixedUpdate();

            TrackProjection parked = track.Project(car.transform.position);
            Assert.That(parked.LateralOffset, Is.EqualTo(shape.HalfWidth - 1.5f).Within(0.3f),
                "The test failed to place the car beside the Barrier (lateral " + parked.LateralOffset + " m).");

            for (int i = 0; i < 150; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Vector3 eye = camera.transform.position;
            Transform barriers = Generated(track, "Barriers");

            // The camera took the bait: it sits beyond the car, on the
            // Barrier side, over the verge the Barrier separates it from.
            float cameraLateral = track.Project(eye).LateralOffset;
            Assert.That(cameraLateral, Is.GreaterThan(parked.LateralOffset + 2f),
                "The camera should fall behind the car, on the Barrier side (lateral " +
                cameraLateral + " m).");

            // Passing through a Barrier means being inside the wall, or having
            // the wall between the eye and the car. Either one blinks the view.
            Assert.That(IsInsideBarrier(track, eye, shape), Is.False,
                "The camera passed through a Barrier at " + eye + ".");
            Assert.That(BarrierBetween(barriers, car.transform.position + Vector3.up * 1.1f, eye), Is.False,
                "A Barrier sits between the camera and the car.");
        }

        /// <summary>Whether a point has ended up inside a Barrier wall.</summary>
        private static bool IsInsideBarrier(TrackRoot track, Vector3 point, TrackShape shape)
        {
            TrackProjection projection = track.Project(point);
            float lateral = Mathf.Abs(projection.LateralOffset);
            float height = point.y - track.transform.position.y;
            return lateral > shape.BarrierOffset - 0.1f
                   && lateral < shape.BarrierOffset + shape.BarrierThickness + 0.1f
                   && height > -0.1f
                   && height < shape.BarrierHeight + 0.1f;
        }

        /// <summary>Whether a Barrier mesh sits between two points.</summary>
        private static bool BarrierBetween(Transform barriers, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.001f)
            {
                return false;
            }

            foreach (RaycastHit hit in Physics.RaycastAll(from, delta / distance, distance,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.transform == barriers)
                {
                    return true;
                }
            }

            return false;
        }

        [UnityTest]
        public IEnumerator GrassSlowsTheCarInTheBuiltScene()
        {
            yield return LoadMainScene();

            TrackRoot track = FindTrack();
            TrackShape shape = track.Shape;
            CarController car = Object.FindAnyObjectByType<CarController>();
            Assert.That(car, Is.Not.Null, "The Main scene must contain a drivable car.");
            DisablePlayerInput();

            Rigidbody body = car.GetComponent<Rigidbody>();

            // The verge has to be wide enough to put the whole car on grass,
            // or the spec's "grass slows the car" can never be seen in play.
            float grassLateral = shape.EdgeOffset + 1.5f;
            Assert.That(grassLateral + 1f, Is.LessThan(shape.BarrierOffset),
                "The Barrier is too close to the Kerb for a car to reach the grass.");

            // Accelerate from rest on each surface. The same throttle input
            // has to produce a visibly worse result on grass.
            Pose pose = track.SamplePose(10f);
            Vector3 right = pose.rotation * Vector3.right;

            Teleport(body, pose.position + Vector3.up * 0.1f, pose.rotation);
            yield return new WaitForFixedUpdate();
            car.SetInput(new HandlingInput { Throttle = 1f });
            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            float roadSpeed = body.linearVelocity.magnitude;

            Teleport(body, pose.position + right * grassLateral + Vector3.up * 0.1f, pose.rotation);
            yield return new WaitForFixedUpdate();
            Assert.That(track.SurfaceAt(car.transform.position), Is.EqualTo(TrackSurface.Grass),
                "The verge between the Kerb and the Barrier should be grass.");
            car.SetInput(new HandlingInput { Throttle = 1f });
            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            float grassSpeed = body.linearVelocity.magnitude;

            Assert.That(roadSpeed, Is.GreaterThan(8f),
                "Two seconds of throttle on the road should get the car moving.");
            Assert.That(grassSpeed, Is.LessThan(roadSpeed * 0.6f),
                "Grass should slow the car noticeably (" +
                roadSpeed.ToString("0.0") + " m/s vs " + grassSpeed.ToString("0.0") + " m/s).");
        }

        [UnityTest]
        public IEnumerator CarCanDriveAFullLapWithoutLeavingTheTrack()
        {
            yield return LoadMainScene();

            TrackRoot track = FindTrack();
            TrackPath path = track.Path;
            TrackShape shape = track.Shape;
            CarController car = Object.FindAnyObjectByType<CarController>();
            Assert.That(car, Is.Not.Null, "The Main scene must contain a drivable car.");
            DisablePlayerInput();

            var body = car.GetComponent<Rigidbody>();
            yield return new WaitForFixedUpdate();

            float previousDistance = track.Project(car.transform.position).Distance;
            float progress = 0f;

            const int maxSteps = 12000;
            int steps = 0;
            float maxLateral = 0f;
            float maxCameraLateral = 0f;
            float minCameraDistance = float.MaxValue;
            float maxCameraDistance = 0f;

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);

            while (progress < path.Length && steps < maxSteps)
            {
                TrackProjection here = track.Project(car.transform.position);
                float distance = here.Distance;
                maxLateral = Mathf.Max(maxLateral, Mathf.Abs(here.LateralOffset));

                float cameraDistance = Vector3.Distance(camera.transform.position, car.transform.position);
                minCameraDistance = Mathf.Min(minCameraDistance, cameraDistance);
                maxCameraDistance = Mathf.Max(maxCameraDistance, cameraDistance);
                maxCameraLateral = Mathf.Max(maxCameraLateral, Mathf.Abs(
                    track.Project(camera.transform.position).LateralOffset));

                float advance = distance - previousDistance;
                if (advance > path.Length * 0.5f)
                {
                    advance -= path.Length;
                }
                else if (advance < -path.Length * 0.5f)
                {
                    advance += path.Length;
                }

                progress += advance;
                previousDistance = distance;

                car.SetInput(Autopilot(track, car, distance));
                yield return new WaitForFixedUpdate();
                steps++;
            }

            Assert.That(progress, Is.GreaterThanOrEqualTo(path.Length),
                $"The car only covered {progress:0} of {path.Length:0} metres in {steps} steps.");

            Assert.That(maxLateral, Is.LessThan(shape.BarrierOffset),
                $"The car reached {maxLateral:0.0}m from the Centreline, past the Barrier.");

            Assert.That(maxCameraLateral, Is.LessThan(shape.BarrierOffset),
                $"The chase camera swung {maxCameraLateral:0.0}m from the Centreline, out past the Barrier.");

            Assert.That(minCameraDistance, Is.GreaterThan(4f),
                "The chase camera dived into the car at some point on the lap.");
            Assert.That(maxCameraDistance, Is.LessThan(12f),
                "The chase camera fell too far behind the car at some point on the lap.");

            float seconds = steps * Time.fixedDeltaTime;
            Assert.That(path.Length / seconds, Is.GreaterThan(20f),
                $"A full lap took {seconds:0} seconds, which is too slow to count as driving at speed.");
        }

        /// <summary>
        /// A simple centreline follower: steam toward a point ahead, and slow
        /// down for whatever the road does beyond it. It is dumb, but a lap it
        /// completes is a lap the Track allows.
        /// </summary>
        private static HandlingInput Autopilot(TrackRoot track, CarController car, float distance)
        {
            TrackPath path = track.Path;
            Vector3 position = track.transform.InverseTransformPoint(car.transform.position);
            Vector3 forward = track.transform.InverseTransformDirection(car.transform.forward);
            float speed = car.GetComponent<Rigidbody>().linearVelocity.magnitude;

            float lookahead = Mathf.Clamp(speed * 0.7f, 10f, 28f);
            Vector3 target = path.SamplePosition(distance + lookahead) - position;
            target.y = 0f;

            float steer = Vector3.Angle(forward, target) < 1f
                ? 0f
                : Mathf.Clamp(Vector3.SignedAngle(forward, target, Vector3.up) / 15f, -1f, 1f);

            Vector3 near = path.SampleDirection(distance + 6f);
            Vector3 far = path.SampleDirection(distance + 24f);
            float curvature = Vector3.Angle(near, far) * Mathf.Deg2Rad / 18f;
            float corneringSpeed = Mathf.Sqrt(9f / Mathf.Max(curvature, 0.0001f));
            float targetSpeed = Mathf.Min(corneringSpeed, 45f);

            float error = targetSpeed - speed;
            return new HandlingInput
            {
                Steer = steer,
                Throttle = Mathf.Clamp01(error / 4f),
                Brake = Mathf.Clamp01(-error / 6f)
            };
        }
    }
}
