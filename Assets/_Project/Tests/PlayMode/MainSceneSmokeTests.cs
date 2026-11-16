using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unbounded.AI;
using Unbounded.App;
using Unbounded.Race;
using Unbounded.Track;
using Unbounded.UI;
using Unbounded.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Unbounded.Tests.PlayMode
{
    /// <summary>
    /// Drives the generated circuit in the real Main scene. Mesh generation,
    /// rendering, and the chase camera are verified here rather than in
    /// EditMode, because they only exist once the scene is playing.
    /// </summary>
    public sealed class MainSceneSmokeTests
    {
        private static IEnumerator LoadMainScene(bool keepOpponents = false)
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            if (!keepOpponents)
            {
                ClearTheField();
                yield return null;
            }
        }

        /// <summary>
        /// Removes the AI field so a test can drive the player car alone. The
        /// opponents race the same scene, so leaving them in would put their
        /// cars in the player's way and make "the" car ambiguous.
        /// </summary>
        private static void ClearTheField()
        {
            foreach (AiDriver driver in Object.FindObjectsByType<AiDriver>())
            {
                Object.Destroy(driver.gameObject);
            }
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
        public IEnumerator TheFieldStartsOnTheGridAndRacesTheCircuit()
        {
            yield return LoadMainScene(keepOpponents: true);

            TrackRoot track = FindTrack();
            RaceController race = Object.FindAnyObjectByType<RaceController>();
            LapProgressTracker player = Object.FindAnyObjectByType<LapProgressTracker>();
            AiDriver[] field = Object.FindObjectsByType<AiDriver>();

            Assert.That(race, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(field.Length, Is.EqualTo(RaceGrid.OpponentCount),
                "Five opponents must line up against the player.");

            // The Grid is on the Road, the player starts behind the field, and
            // nobody is close enough to spawn on top of anybody else.
            var starts = new List<Vector3> { player.transform.position };
            foreach (AiDriver opponent in field)
            {
                Vector3 position = opponent.transform.position;
                TrackProjection projection = track.Project(position);

                Assert.That(track.SurfaceAt(position), Is.EqualTo(TrackSurface.Road),
                    "Every opponent must start on the Road.");
                Assert.That(Mathf.Abs(projection.LateralOffset), Is.LessThan(track.Shape.HalfWidth - 1f),
                    "Every opponent must start within the Road.");
                Assert.That(opponent.Progress, Is.GreaterThan(player.Progress),
                    "The player starts behind the field.");
                starts.Add(position);
            }

            for (int a = 0; a < starts.Count; a++)
            {
                for (int b = a + 1; b < starts.Count; b++)
                {
                    Vector3 apart = starts[a] - starts[b];
                    apart.y = 0f;
                    Assert.That(apart.magnitude, Is.GreaterThan(4f),
                        "Two cars spawned on top of each other on the Grid.");
                }
            }

            float deadline = Time.unscaledTime + 8f;
            while (race.Race.Phase == RacePhase.Countdown && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            Assert.That(race.Race.Phase, Is.EqualTo(RacePhase.Racing),
                "The Countdown must release the field on its own.");

            float furthest = 0f;
            for (int step = 0; step < 300; step++)
            {
                yield return new WaitForFixedUpdate();
                foreach (AiDriver opponent in field)
                {
                    furthest = Mathf.Max(furthest,
                        Mathf.Abs(track.Project(opponent.transform.position).LateralOffset));
                }
            }

            for (int i = 0; i < field.Length; i++)
            {
                AiDriver opponent = field[i];
                Rigidbody body = opponent.GetComponent<Rigidbody>();

                Assert.That(Vector3.Distance(starts[i + 1], opponent.transform.position), Is.GreaterThan(60f),
                    "An opponent barely moved off the Grid.");
                Assert.That(body.linearVelocity.magnitude, Is.GreaterThan(5f),
                    "An opponent stalled on the Track.");
                Assert.That(opponent.ControlsEnabled, Is.True,
                    "An opponent lost its controls while racing.");
            }

            Assert.That(furthest, Is.LessThan(track.Shape.EdgeOffset),
                $"An opponent left the Road and the Kerb (reached {furthest:0.0}m from the Centreline).");
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
        public IEnumerator BarrierHoldsAFastObliqueHit()
        {
            yield return LoadMainScene();

            TrackRoot track = FindTrack();
            TrackShape shape = track.Shape;
            CarController car = Object.FindAnyObjectByType<CarController>();
            Assert.That(car, Is.Not.Null, "The Main scene must contain a drivable car.");
            DisablePlayerInput();

            Rigidbody body = car.GetComponent<Rigidbody>();

            // A glancing blow at speed is the case most likely to tunnel through
            // a 0.4 m wall: 30 m/s, 30 degrees off the right-hand Barrier.
            Pose pose = track.SamplePose(20f);
            Vector3 right = pose.rotation * Vector3.right;
            const float speed = 30f;

            Teleport(body,
                pose.position + right * 4f + Vector3.up * 0.1f,
                Quaternion.AngleAxis(30f, Vector3.up) * pose.rotation);
            car.SetInput(default);
            yield return new WaitForFixedUpdate();

            body.linearVelocity = body.rotation * Vector3.forward * speed;
            Assert.That(body.linearVelocity.magnitude, Is.EqualTo(speed).Within(0.5f));

            float deepest = float.MinValue;
            float lowest = float.MaxValue;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
                deepest = Mathf.Max(deepest, RightmostCornerLateral(track, body));
                lowest = Mathf.Min(lowest, body.position.y);
            }

            Assert.That(deepest, Is.GreaterThan(shape.EdgeOffset),
                "The car should have reached the Barrier, not stopped short (max lateral " +
                deepest.ToString("0.0") + " m).");
            Assert.That(deepest, Is.LessThan(shape.BarrierOffset + 0.15f),
                "Part of the car got through the Barrier (max lateral " + deepest.ToString("0.0") + " m).");
            Assert.That(lowest, Is.GreaterThan(-0.5f),
                "The car fell through the world at the Barrier (lowest y " + lowest.ToString("0.0") + ").");

            // The wall cancels the speed into it but leaves the car sliding
            // along it, rather than sticking dead or being thrown back.
            float intoWall = Vector3.Dot(body.linearVelocity, right);
            Assert.That(intoWall, Is.LessThan(1f),
                "The Barrier should stop the car heading through it (lateral speed " +
                intoWall.ToString("0.0") + " m/s).");
            Assert.That(body.linearVelocity.magnitude, Is.GreaterThan(5f),
                "The car should keep sliding along the Barrier instead of sticking to it.");

            // Bring the car to a stop against the wall, then reverse away. A car
            // that cannot get off the wall is trapped even though the wall held.
            car.SetInput(new HandlingInput { Brake = 1f });
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Vector3 stopped = body.position;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(Vector3.Distance(stopped, body.position), Is.GreaterThan(2f),
                "The car should be able to drive away from the Barrier instead of being trapped.");
        }

        /// <summary>The lateral offset of the car's four collider corners, furthest to the right.</summary>
        private static float RightmostCornerLateral(TrackRoot track, Rigidbody body)
        {
            var collider = body.GetComponent<BoxCollider>();
            Vector3 centre = collider.center;
            float deepest = float.MinValue;

            foreach (int side in new[] { -1, 1 })
            {
                foreach (int end in new[] { -1, 1 })
                {
                    Vector3 corner = body.position + body.rotation * (centre + new Vector3(
                        collider.size.x * 0.5f * side, 0f, collider.size.z * 0.5f * end));
                    deepest = Mathf.Max(deepest, track.Project(corner).LateralOffset);
                }
            }

            return deepest;
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

        [UnityTest]
        public IEnumerator ChaseCameraPullsInToAvoidAWallBehindTheCar()
        {
            yield return LoadMainScene();

            TrackRoot track = FindTrack();
            CarController car = Object.FindAnyObjectByType<CarController>();
            Assert.That(car, Is.Not.Null, "The Main scene must contain a drivable car.");
            DisablePlayerInput();

            Rigidbody body = car.GetComponent<Rigidbody>();
            Pose pose = track.SamplePose(20f);
            Teleport(body, pose.position + Vector3.up * 0.1f, pose.rotation);
            car.SetInput(default);
            yield return new WaitForFixedUpdate();

            Assert.That(Camera.main, Is.Not.Null);
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            float natural = CameraBehind(car);
            Assert.That(natural, Is.GreaterThan(5f),
                "The chase camera should sit well behind the car when nothing is in the way (" +
                natural.ToString("0.0") + " m).");

            // A wall just beyond where the camera wants to be. The obstacles are
            // low enough on the real Track that the avoidance rarely has to fire,
            // so this temporary wall is what proves it is wired up.
            const float wallOffset = 4.75f;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Test Wall";
            wall.transform.SetPositionAndRotation(
                car.transform.position - car.transform.forward * wallOffset + Vector3.up * 2.5f,
                car.transform.rotation);
            wall.transform.localScale = new Vector3(4f, 5f, 1f);

            try
            {
                for (int i = 0; i < 150; i++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float pulledIn = CameraBehind(car);
                Assert.That(pulledIn, Is.LessThan(wallOffset - 0.55f),
                    "The camera stayed behind the wall at " + pulledIn.ToString("0.0") + " m.");
                Assert.That(pulledIn, Is.GreaterThan(1.5f),
                    "The camera should pull in, not collapse onto the car (" +
                    pulledIn.ToString("0.0") + " m).");
            }
            finally
            {
                Object.Destroy(wall);
            }
        }

        /// <summary>How far behind the car the camera is, along the car's forward axis.</summary>
        private static float CameraBehind(CarController car) =>
            -Vector3.Dot(Camera.main.transform.position - car.transform.position,
                car.transform.forward);

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

            LapProgressTracker lapProgress = Object.FindAnyObjectByType<LapProgressTracker>();
            Assert.That(lapProgress, Is.Not.Null, "The car must track its Lap progress.");
            Assert.That(lapProgress.LapsCompleted, Is.GreaterThanOrEqualTo(1),
                "Driving the whole circuit must count a Lap.");

            RaceHud hud = Object.FindAnyObjectByType<RaceHud>();
            Text lapText = hud.transform.Find("Lap Counter").GetComponent<Text>();
            Assert.That(lapText.text, Is.EqualTo("LAP 2/3"),
                "The HUD must advance to the next Lap once the line is crossed.");
        }

        [UnityTest]
        public IEnumerator HudShowsTheLapAndWarnsWhenTheCarTurnsAround()
        {
            yield return LoadMainScene(keepOpponents: true);

            RaceHud hud = Object.FindAnyObjectByType<RaceHud>();
            Assert.That(hud, Is.Not.Null, "The Main scene must contain the HUD.");

            Text lapText = hud.transform.Find("Lap Counter").GetComponent<Text>();
            Text positionText = hud.transform.Find("Position").GetComponent<Text>();
            Text wrongWayText = hud.transform.Find("Wrong Way").GetComponent<Text>();
            Assert.That(lapText, Is.Not.Null);
            Assert.That(positionText, Is.Not.Null);
            Assert.That(wrongWayText, Is.Not.Null);

            Assert.That(lapText.text, Is.EqualTo("LAP 1/3"),
                "The HUD must show the Lap being driven and the total from the start.");
            Assert.That(positionText.text, Is.EqualTo("POS 6/6"),
                "The player starts at the back of a six-car Grid, and the HUD must say so.");

            // The rest of this test drives the player alone.
            ClearTheField();
            yield return null;
            Assert.That(wrongWayText.gameObject.activeSelf, Is.False,
                "No wrong-way warning before the car has moved.");

            TrackRoot track = FindTrack();
            CarController car = Object.FindAnyObjectByType<CarController>();
            Assert.That(car, Is.Not.Null);
            DisablePlayerInput();

            Rigidbody body = car.GetComponent<Rigidbody>();
            Pose pose = track.SamplePose(30f);

            Teleport(body, pose.position + Vector3.up * 0.1f, pose.rotation * Quaternion.Euler(0f, 180f, 0f));
            yield return new WaitForFixedUpdate();

            car.SetInput(new HandlingInput { Throttle = 1f });
            for (int i = 0; i < 80; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(wrongWayText.gameObject.activeSelf, Is.True,
                "Driving against the Track direction must show the warning.");

            Teleport(body, pose.position + Vector3.up * 0.1f, pose.rotation);
            car.SetInput(new HandlingInput { Throttle = 1f });
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(wrongWayText.gameObject.activeSelf, Is.False,
                "The warning must clear once the car faces along the Track again.");
        }

        [UnityTest]
        public IEnumerator ResetReturnsTheCarToTheLastCheckpointItPassed()
        {
            yield return LoadMainScene();

            TrackRoot track = FindTrack();
            Assert.That(track.Checkpoints, Is.Not.Null, "The Track must expose its Checkpoints.");

            LapProgressTracker tracker = Object.FindAnyObjectByType<LapProgressTracker>();
            CarController car = Object.FindAnyObjectByType<CarController>();
            Assert.That(tracker, Is.Not.Null, "The car must track its Lap progress.");
            Assert.That(car, Is.Not.Null);
            DisablePlayerInput();

            Rigidbody body = car.GetComponent<Rigidbody>();
            float checkpoint = track.Checkpoints.DistanceAt(0);

            // Move along the Track in steps the Lap counter accepts, so the
            // first Checkpoint is genuinely passed.
            for (float distance = 5f; distance <= checkpoint + 25f; distance += 20f)
            {
                Pose step = track.SamplePose(distance);
                Teleport(body, step.position + Vector3.up * 0.1f, step.rotation);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(tracker.ResetDistance, Is.EqualTo(checkpoint).Within(1f),
                "The reset point must be the last Checkpoint passed.");

            Pose away = track.SamplePose(checkpoint + 80f);
            Teleport(body, away.position + Vector3.up * 0.1f, away.rotation);
            body.linearVelocity = away.rotation * Vector3.forward * 15f;
            yield return new WaitForFixedUpdate();

            tracker.ResetCar();
            yield return new WaitForFixedUpdate();

            float resetDistance = track.Project(body.position).Distance;
            Assert.That(resetDistance, Is.EqualTo(checkpoint).Within(1f),
                "Reset must return the car to the last Checkpoint, not the line.");
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(0.5f),
                "Reset must leave the car at rest.");
            Assert.That(Vector3.Dot(body.rotation * Vector3.forward,
                track.SamplePose(checkpoint).rotation * Vector3.forward), Is.GreaterThan(0.99f),
                "Reset must point the car along the Track.");
        }

        [UnityTest]
        public IEnumerator RaceStartsWithACountdownThenRunsTheClock()
        {
            yield return LoadMainScene();

            RaceController race = Object.FindAnyObjectByType<RaceController>();
            Assert.That(race, Is.Not.Null, "The Main scene must contain the Race flow.");
            Assert.That(race.Race, Is.Not.Null);

            RaceHud hud = Object.FindAnyObjectByType<RaceHud>();
            Text countdown = hud.transform.Find("Countdown").GetComponent<Text>();
            Text timer = hud.transform.Find("Race Timer").GetComponent<Text>();
            PlayerInputReader input = Object.FindAnyObjectByType<PlayerInputReader>();
            Assert.That(countdown, Is.Not.Null);
            Assert.That(timer, Is.Not.Null);
            Assert.That(input, Is.Not.Null);

            Assert.That(race.Race.Phase, Is.EqualTo(RacePhase.Countdown));
            Assert.That(race.Race.RaceTime, Is.Zero, "The clock must not run before the Countdown ends.");
            Assert.That(countdown.gameObject.activeSelf, Is.True, "The Countdown must be on screen.");
            Assert.That(countdown.text, Is.EqualTo("3"));
            Assert.That(input.ControlsEnabled, Is.False, "Driving input must be ignored during the Countdown.");

            float countdownDeadline = Time.unscaledTime + 6f;
            while (race.Race.Phase == RacePhase.Countdown && Time.unscaledTime < countdownDeadline)
            {
                yield return null;
            }

            Assert.That(race.Race.Phase, Is.EqualTo(RacePhase.Racing),
                "The Countdown must end on its own.");
            Assert.That(input.ControlsEnabled, Is.True, "Driving input must reach the car once the Race is running.");
            Assert.That(countdown.text, Is.EqualTo("GO!"), "The start should read as a go, not a silent cut.");

            float clockDeadline = Time.unscaledTime + 2f;
            while (timer.text == "0:00.00" && Time.unscaledTime < clockDeadline)
            {
                yield return null;
            }

            Assert.That(timer.text, Is.Not.EqualTo("0:00.00"),
                "The Race timer must run from the end of the Countdown.");

            float goDeadline = Time.unscaledTime + 3f;
            while (countdown.gameObject.activeSelf && Time.unscaledTime < goDeadline)
            {
                yield return null;
            }

            Assert.That(countdown.gameObject.activeSelf, Is.False,
                "The Countdown display must clear once the Race is under way.");
        }

        [UnityTest]
        public IEnumerator RestartReturnsTheCarAndTheLapCounterToTheStart()
        {
            yield return LoadMainScene();

            RaceController race = Object.FindAnyObjectByType<RaceController>();
            LapProgressTracker tracker = Object.FindAnyObjectByType<LapProgressTracker>();
            CarController car = Object.FindAnyObjectByType<CarController>();
            Assert.That(race, Is.Not.Null);
            Assert.That(tracker, Is.Not.Null);
            Assert.That(car, Is.Not.Null);
            DisablePlayerInput();

            float startDeadline = Time.unscaledTime + 6f;
            while (race.Race.Phase == RacePhase.Countdown && Time.unscaledTime < startDeadline)
            {
                yield return null;
            }

            Assert.That(race.Race.Phase, Is.EqualTo(RacePhase.Racing));

            car.SetInput(new HandlingInput { Throttle = 1f });
            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Vector3 moved = car.transform.position;
            car.SetInput(default);

            race.RestartRace();
            yield return new WaitForFixedUpdate();
            yield return null;

            TrackRoot track = FindTrack();
            Pose start = track.SamplePose(LapProgressTracker.StartDistance);
            Vector3 offset = car.transform.position - start.position;
            offset.y = 0f;

            Assert.That(race.Race.Phase, Is.EqualTo(RacePhase.Countdown), "Restart must return to the Countdown.");
            Assert.That(race.Race.RaceTime, Is.Zero);
            Assert.That(race.Race.LapsCompleted, Is.Zero);
            Assert.That(race.Race.LapTimes, Is.Empty);
            Assert.That(tracker.LapsCompleted, Is.Zero);
            Assert.That(offset.magnitude, Is.LessThan(3f),
                "Restart must put the car back on the Grid without reloading the scene.");
            Assert.That(Vector3.Distance(moved, car.transform.position), Is.GreaterThan(5f),
                "The test failed to move the car before restarting.");
            Assert.That(car.GetComponent<Rigidbody>().linearVelocity.magnitude, Is.LessThan(0.5f),
                "Restart must leave the car at rest.");

            RaceHud hud = Object.FindAnyObjectByType<RaceHud>();
            Text lapText = hud.transform.Find("Lap Counter").GetComponent<Text>();
            Assert.That(lapText.text, Is.EqualTo("LAP 1/3"),
                "Restart must clear the previous Laps from the HUD too.");
        }

        [UnityTest]
        public IEnumerator TheFinishScreenAppearsAfterTheFinalLap()
        {
            yield return LoadMainScene(keepOpponents: true);

            RaceController race = Object.FindAnyObjectByType<RaceController>();
            RaceHud hud = Object.FindAnyObjectByType<RaceHud>();
            Assert.That(race, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            DisablePlayerInput();

            float startDeadline = Time.unscaledTime + 6f;
            while (race.Race.Phase == RacePhase.Countdown && Time.unscaledTime < startDeadline)
            {
                yield return null;
            }

            Assert.That(race.Race.Phase, Is.EqualTo(RacePhase.Racing));

            GameObject finishScreen = hud.transform.Find("Finish Screen").gameObject;
            Text summary = finishScreen.transform.Find("Summary").GetComponent<Text>();
            Assert.That(finishScreen.activeSelf, Is.False, "The finish screen must stay hidden during the Race.");

            // The Race flow records Laps from the validated Lap events; driving
            // three full Laps in real time would only retest the Lap counter.
            for (int lap = 0; lap < 3; lap++)
            {
                race.Race.CompleteLap();
            }

            yield return null;

            Assert.That(race.Race.Phase, Is.EqualTo(RacePhase.Finished));
            Assert.That(finishScreen.activeSelf, Is.True, "The finish screen must appear once the Race ends.");
            Assert.That(summary.text, Does.Contain("TIME"), "The finish screen must show the Race time.");

            for (int place = 1; place <= RaceGrid.OpponentCount; place++)
            {
                Assert.That(summary.text, Does.Contain("OPPONENT " + place),
                    "The finish screen must list every car in the field.");
            }

            Assert.That(summary.text, Does.Contain("YOU"), "The finish screen must list the player.");
            Assert.That(summary.text.IndexOf("OPPONENT " + RaceGrid.OpponentCount),
                Is.LessThan(summary.text.IndexOf("YOU")),
                "The finish screen must list the field in order; from the back of the Grid the player is last.");

            string stopped = hud.transform.Find("Race Timer").GetComponent<Text>().text;
            yield return new WaitForSecondsRealtime(0.4f);

            Assert.That(hud.transform.Find("Race Timer").GetComponent<Text>().text, Is.EqualTo(stopped),
                "The Race timer must stop at the finish.");

            race.RestartRace();
            yield return null;

            Assert.That(race.Race.Phase, Is.EqualTo(RacePhase.Countdown));
            Assert.That(finishScreen.activeSelf, Is.False, "Restart must clear the finish screen.");
        }

        [UnityTest]
        public IEnumerator PauseFreezesTheRaceAndResumeContinuesIt()
        {
            yield return LoadMainScene();

            RaceController race = Object.FindAnyObjectByType<RaceController>();
            CarController car = Object.FindAnyObjectByType<CarController>();
            RaceHud hud = Object.FindAnyObjectByType<RaceHud>();
            Assert.That(race, Is.Not.Null);
            Assert.That(car, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            DisablePlayerInput();

            float startDeadline = Time.unscaledTime + 6f;
            while (race.Race.Phase == RacePhase.Countdown && Time.unscaledTime < startDeadline)
            {
                yield return null;
            }

            car.SetInput(new HandlingInput { Throttle = 1f });
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            GameObject pauseScreen = hud.transform.Find("Pause Screen").gameObject;
            try
            {
                race.SetPaused(true);
                yield return null;

                Assert.That(Time.timeScale, Is.Zero, "Pause must stop the simulation clock.");
                Assert.That(race.IsPaused, Is.True);
                Assert.That(pauseScreen.activeSelf, Is.True, "Pause must show the pause screen.");

                Vector3 held = car.transform.position;
                float heldTime = race.Race.RaceTime;

                yield return new WaitForSecondsRealtime(0.4f);

                Assert.That(car.transform.position, Is.EqualTo(held),
                    "Pause must freeze the car where it was.");
                Assert.That(race.Race.RaceTime, Is.EqualTo(heldTime),
                    "Pause must freeze the Race timer.");

                race.SetPaused(false);
                yield return null;

                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(race.IsPaused, Is.False);
                Assert.That(pauseScreen.activeSelf, Is.False);

                Vector3 resumed = car.transform.position;
                for (int i = 0; i < 60; i++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(race.Race.RaceTime, Is.GreaterThan(heldTime),
                    "Resume must continue the Race timer from where it stopped.");
                Assert.That(Vector3.Distance(resumed, car.transform.position), Is.GreaterThan(2f),
                    "Resume must continue the Race from the same state, not from rest.");
            }
            finally
            {
                race.SetPaused(false);
                Time.timeScale = 1f;
            }
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
