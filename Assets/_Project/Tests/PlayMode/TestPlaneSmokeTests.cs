using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unbounded.App;
using Unbounded.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unbounded.Tests.PlayMode
{
    public sealed class TestPlaneSmokeTests
    {
        [UnityTest]
        public IEnumerator MainSceneCarMovesAndLogsNoErrors()
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
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;

                CarController car = Object.FindAnyObjectByType<CarController>();
                Assert.That(car, Is.Not.Null, "The Main scene must contain a drivable car.");

                foreach (PlayerInputReader reader in Object.FindObjectsByType<PlayerInputReader>())
                {
                    reader.enabled = false;
                }

                yield return new WaitForFixedUpdate();
                Vector3 start = car.transform.position;

                car.SetInput(new HandlingInput { Throttle = 1f });

                for (int i = 0; i < 120; i++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float distance = Vector3.Distance(start, car.transform.position);
                Assert.That(distance, Is.GreaterThan(5f), "Throttle should move the car across the test plane.");

                Camera camera = Camera.main;
                Assert.That(camera, Is.Not.Null);
                Vector3 cameraOffset = camera.transform.position - car.transform.position;
                Assert.That(Vector3.Dot(cameraOffset, car.transform.forward), Is.LessThan(0f),
                    "The chase camera should sit behind the car.");
                Assert.That(cameraOffset.magnitude, Is.LessThan(12f),
                    "The chase camera should stay close enough to read the road ahead.");

                car.SetInput(new HandlingInput { Throttle = 0.5f, Steer = 1f });
                for (int i = 0; i < 30; i++)
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
    }
}
