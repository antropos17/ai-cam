using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugCam.Core
{
    public static class BugCamConstants
    {
        public const float FixedStep = 0.02f;
        public const int StateStride = 14;
        public const float RepeatabilityGate = 1e-6f;
    }

    /// <summary>
    /// Block 2.2.1 A2 primitive collider shapes. Box is the default (all pre-A2 call
    /// sites); Sphere interprets <see cref="SimulationBodyDefinition.Size"/> as a uniform
    /// diameter vector (ratified: sphere Size = diameter).
    /// </summary>
    public enum SimulationColliderShape
    {
        Box = 0,
        Sphere = 1
    }

    public readonly struct SimulationBodyDefinition
    {
        public SimulationBodyDefinition(
            int stableId,
            Vector3 position,
            Quaternion rotation,
            Vector3 size,
            float mass)
            : this(stableId, position, rotation, size, mass, Vector3.zero)
        {
        }

        public SimulationBodyDefinition(
            int stableId,
            Vector3 position,
            Quaternion rotation,
            Vector3 size,
            float mass,
            Vector3 initialLinearVelocity)
            : this(
                stableId,
                position,
                rotation,
                size,
                mass,
                initialLinearVelocity,
                SimulationColliderShape.Box)
        {
        }

        public SimulationBodyDefinition(
            int stableId,
            Vector3 position,
            Quaternion rotation,
            Vector3 size,
            float mass,
            Vector3 initialLinearVelocity,
            SimulationColliderShape shape)
        {
            StableId = stableId;
            Position = position;
            Rotation = rotation;
            Size = size;
            Mass = mass;
            InitialLinearVelocity = initialLinearVelocity;
            Shape = shape;
        }

        public int StableId { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public Vector3 Size { get; }

        public float Mass { get; }

        public Vector3 InitialLinearVelocity { get; }

        public SimulationColliderShape Shape { get; }
    }

    /// <summary>
    /// Block 2.2.1 A2: one captured static collider (including kinematic bodies frozen to
    /// static). Recreated in every temporary physics scene exactly as recorded.
    /// </summary>
    public readonly struct SimulationStaticColliderDefinition
    {
        public SimulationStaticColliderDefinition(
            Vector3 position,
            Quaternion rotation,
            Vector3 size,
            SimulationColliderShape shape)
        {
            Position = position;
            Rotation = rotation;
            Size = size;
            Shape = shape;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public Vector3 Size { get; }

        public SimulationColliderShape Shape { get; }
    }

    public readonly struct SimulationPerturbation
    {
        public SimulationPerturbation(int targetBodyId, Vector3 axis, float magnitudeMetres)
        {
            TargetBodyId = targetBodyId;
            Axis = axis == Vector3.zero ? Vector3.zero : axis.normalized;
            MagnitudeMetres = magnitudeMetres;
        }

        public int TargetBodyId { get; }

        public Vector3 Axis { get; }

        public float MagnitudeMetres { get; }
    }

    public readonly struct SimulationRequest
    {
        public SimulationRequest(
            SimulationBodyDefinition[] bodies,
            int stepCount,
            SimulationPerturbation perturbation)
            : this(bodies, stepCount, perturbation, null)
        {
        }

        public SimulationRequest(
            SimulationBodyDefinition[] bodies,
            int stepCount,
            SimulationPerturbation perturbation,
            SimulationStaticColliderDefinition[] staticColliders)
        {
            Bodies = bodies;
            StepCount = stepCount;
            Perturbation = perturbation;
            StaticColliders = staticColliders;
        }

        public SimulationBodyDefinition[] Bodies { get; }

        public int StepCount { get; }

        public SimulationPerturbation Perturbation { get; }

        /// <summary>
        /// A2: null = legacy procedural ground (tower path, bit-identical to pre-A2);
        /// non-null (possibly empty) = create exactly these captured statics and no
        /// implicit ground.
        /// </summary>
        public SimulationStaticColliderDefinition[] StaticColliders { get; }
    }

    public readonly struct SimulationRunResult
    {
        private static readonly Vector3[] EmptyPositions = Array.Empty<Vector3>();
        private static readonly int[] EmptyStableBodyIds = Array.Empty<int>();
        private static readonly float[] EmptyStateFrames = Array.Empty<float>();

        private SimulationRunResult(
            bool succeeded,
            string errorReason,
            Vector3[] finalBodyPositions,
            int[] stableBodyIds,
            SimulationPerturbation appliedPerturbation,
            float[] stateFrames,
            long managedBytesAllocatedInLoop,
            bool localPhysicsSceneWasValid,
            bool temporarySceneUnloadRequested)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason;
            FinalBodyPositions = finalBodyPositions;
            StableBodyIds = stableBodyIds;
            AppliedPerturbation = appliedPerturbation;
            StateFrames = stateFrames;
            ManagedBytesAllocatedInLoop = managedBytesAllocatedInLoop;
            LocalPhysicsSceneWasValid = localPhysicsSceneWasValid;
            TemporarySceneUnloadRequested = temporarySceneUnloadRequested;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public Vector3[] FinalBodyPositions { get; }

        public int[] StableBodyIds { get; }

        public SimulationPerturbation AppliedPerturbation { get; }

        public float[] StateFrames { get; }

        public long ManagedBytesAllocatedInLoop { get; }

        public bool LocalPhysicsSceneWasValid { get; }

        public bool TemporarySceneUnloadRequested { get; }

        internal static SimulationRunResult Success(
            Vector3[] finalBodyPositions,
            int[] stableBodyIds,
            SimulationPerturbation appliedPerturbation,
            float[] stateFrames,
            long managedBytesAllocatedInLoop,
            bool localPhysicsSceneWasValid,
            bool temporarySceneUnloadRequested)
        {
            return new SimulationRunResult(
                true,
                string.Empty,
                finalBodyPositions,
                stableBodyIds,
                appliedPerturbation,
                stateFrames,
                managedBytesAllocatedInLoop,
                localPhysicsSceneWasValid,
                temporarySceneUnloadRequested);
        }

        internal static SimulationRunResult Failure(string errorReason)
        {
            return new SimulationRunResult(
                false,
                errorReason,
                EmptyPositions,
                EmptyStableBodyIds,
                default,
                EmptyStateFrames,
                0L,
                false,
                false);
        }
    }

    public sealed class SimulationHarness
    {
        private static int nextSceneId;

        public SimulationRunResult Run(SimulationRequest request)
        {
            if (Physics.simulationMode != SimulationMode.Script)
            {
                return SimulationRunResult.Failure(
                    "Physics.simulationMode must be Script before simulation begins.");
            }

            if (request.Bodies == null || request.Bodies.Length == 0)
            {
                return SimulationRunResult.Failure("At least one simulation body is required.");
            }

            if (request.StepCount <= 0)
            {
                return SimulationRunResult.Failure("StepCount must be greater than zero.");
            }

            var orderedBodies = CopyBodiesInStableIdOrder(request.Bodies);
            for (var bodyIndex = 0; bodyIndex < orderedBodies.Length; bodyIndex++)
            {
                var body = orderedBodies[bodyIndex];
                if (!IsValid(body))
                {
                    return SimulationRunResult.Failure(
                        "Every body needs finite position, rotation, and initial velocity plus positive finite size and mass.");
                }

                if (bodyIndex > 0 && orderedBodies[bodyIndex - 1].StableId == body.StableId)
                {
                    return SimulationRunResult.Failure("Body StableId values must be unique.");
                }
            }

            var appliedPerturbation = request.Perturbation;
            if (appliedPerturbation.MagnitudeMetres != 0f)
            {
                if (!IsPositiveFinite(appliedPerturbation.MagnitudeMetres) ||
                    !IsFinite(appliedPerturbation.Axis) ||
                    appliedPerturbation.Axis == Vector3.zero)
                {
                    return SimulationRunResult.Failure(
                        "A perturbation needs a positive finite magnitude and non-zero finite axis.");
                }

                var targetFound = false;
                for (var bodyIndex = 0; bodyIndex < orderedBodies.Length; bodyIndex++)
                {
                    if (orderedBodies[bodyIndex].StableId == appliedPerturbation.TargetBodyId)
                    {
                        targetFound = true;
                        break;
                    }
                }

                if (!targetFound)
                {
                    return SimulationRunResult.Failure(
                        "The perturbation target does not exist in the simulation request.");
                }
            }

            if (request.StaticColliders != null)
            {
                for (var i = 0; i < request.StaticColliders.Length; i++)
                {
                    var collider = request.StaticColliders[i];
                    if (!IsFinite(collider.Position) ||
                        !IsFinite(collider.Rotation) ||
                        !IsPositiveFinite(collider.Size.x) ||
                        !IsPositiveFinite(collider.Size.y) ||
                        !IsPositiveFinite(collider.Size.z))
                    {
                        return SimulationRunResult.Failure(
                            "Every static collider needs finite position and rotation plus positive finite size.");
                    }
                }
            }

            if (!Application.isPlaying)
            {
                return SimulationRunResult.Failure(
                    "SimulationHarness requires Play Mode because it creates an isolated local Physics3D scene.");
            }

            var simulationScene = default(Scene);
            var temporarySceneUnloadRequested = false;
            SimulationRunResult pendingResult = SimulationRunResult.Failure(
                "Simulation did not produce a result.");
            var hasPendingSuccess = false;
            try
            {
                // This harness requires Play Mode because its isolation model depends on
                // SceneManager.CreateScene with LocalPhysicsMode.Physics3D.
                simulationScene = SceneManager.CreateScene(
                    "BugCam Simulation " + nextSceneId++,
                    new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                if (!simulationScene.IsValid() || !simulationScene.isLoaded)
                {
                    return SimulationRunResult.Failure(
                        "Failed to create a loaded local physics simulation scene.");
                }

                var physicsScene = simulationScene.GetPhysicsScene();
                if (!physicsScene.IsValid())
                {
                    return SimulationRunResult.Failure("The local PhysicsScene is invalid.");
                }

                if (request.StaticColliders == null)
                {
                    // Static ground matches TowerSceneGenerator so tower bodies rest/collide
                    // identically to the demo scene. Not counted in the 49 Rigidbody body set.
                    CreateGround(simulationScene);
                }
                else
                {
                    // A2 captured scene: exactly the recorded statics, no implicit ground.
                    for (var staticIndex = 0;
                         staticIndex < request.StaticColliders.Length;
                         staticIndex++)
                    {
                        CreateStaticCollider(
                            simulationScene,
                            request.StaticColliders[staticIndex]);
                    }
                }

                var runtimeBodies = new Rigidbody[orderedBodies.Length];
                for (var bodyIndex = 0; bodyIndex < orderedBodies.Length; bodyIndex++)
                {
                    runtimeBodies[bodyIndex] = CreateBody(
                        simulationScene,
                        orderedBodies[bodyIndex],
                        appliedPerturbation);
                }

                var recorder = StateRecorder.Allocate(
                    runCount: 1,
                    request.StepCount,
                    runtimeBodies.Length);
                var allocatedBytesBeforeLoop = GC.GetAllocatedBytesForCurrentThread();
                for (var step = 0; step < request.StepCount; step++)
                {
                    if (!physicsScene.IsValid())
                    {
                        return SimulationRunResult.Failure(
                            "The local PhysicsScene became invalid before Simulate.");
                    }

                    physicsScene.Simulate(BugCamConstants.FixedStep);
                    recorder.WriteRigidbodies(runIndex: 0, step, runtimeBodies);
                }
                var managedBytesAllocatedInLoop =
                    GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBeforeLoop;

                var finalBodyPositions = new Vector3[runtimeBodies.Length];
                var stableBodyIds = new int[runtimeBodies.Length];
                for (var bodyIndex = 0; bodyIndex < runtimeBodies.Length; bodyIndex++)
                {
                    finalBodyPositions[bodyIndex] = runtimeBodies[bodyIndex].position;
                    stableBodyIds[bodyIndex] = orderedBodies[bodyIndex].StableId;
                }

                pendingResult = SimulationRunResult.Success(
                    finalBodyPositions,
                    stableBodyIds,
                    appliedPerturbation,
                    recorder.Buffer,
                    managedBytesAllocatedInLoop,
                    localPhysicsSceneWasValid: true,
                    temporarySceneUnloadRequested: false);
                hasPendingSuccess = true;
            }
            catch (Exception exception)
            {
                return SimulationRunResult.Failure("Simulation failed: " + exception.Message);
            }
            finally
            {
                if (simulationScene.IsValid() && simulationScene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(simulationScene);
                    temporarySceneUnloadRequested = true;
                }
            }

            if (!hasPendingSuccess)
            {
                return pendingResult;
            }

            return SimulationRunResult.Success(
                pendingResult.FinalBodyPositions,
                pendingResult.StableBodyIds,
                pendingResult.AppliedPerturbation,
                pendingResult.StateFrames,
                pendingResult.ManagedBytesAllocatedInLoop,
                pendingResult.LocalPhysicsSceneWasValid,
                temporarySceneUnloadRequested);
        }

        private static SimulationBodyDefinition[] CopyBodiesInStableIdOrder(
            SimulationBodyDefinition[] bodies)
        {
            var orderedBodies = new SimulationBodyDefinition[bodies.Length];
            for (var bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
            {
                var body = bodies[bodyIndex];
                var insertionIndex = bodyIndex;
                while (insertionIndex > 0 &&
                       orderedBodies[insertionIndex - 1].StableId > body.StableId)
                {
                    orderedBodies[insertionIndex] = orderedBodies[insertionIndex - 1];
                    insertionIndex--;
                }

                orderedBodies[insertionIndex] = body;
            }

            return orderedBodies;
        }

        private static bool IsValid(SimulationBodyDefinition body)
        {
            return IsFinite(body.Position) &&
                   IsFinite(body.Rotation) &&
                   IsFinite(body.InitialLinearVelocity) &&
                   IsPositiveFinite(body.Size.x) &&
                   IsPositiveFinite(body.Size.y) &&
                   IsPositiveFinite(body.Size.z) &&
                   IsPositiveFinite(body.Mass);
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsInfinity(value.x) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsInfinity(value.z) &&
                   !float.IsNaN(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsNaN(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsInfinity(value.x) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsInfinity(value.z) &&
                   !float.IsInfinity(value.w) &&
                   !float.IsNaN(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsNaN(value.w);
        }

        private static void CreateStaticCollider(
            Scene simulationScene,
            SimulationStaticColliderDefinition definition)
        {
            var gameObject = new GameObject("BugCam Static");
            SceneManager.MoveGameObjectToScene(gameObject, simulationScene);
            gameObject.transform.SetPositionAndRotation(definition.Position, definition.Rotation);
            gameObject.transform.localScale = definition.Size;
            if (definition.Shape == SimulationColliderShape.Sphere)
            {
                gameObject.AddComponent<SphereCollider>();
            }
            else
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }

        private static void CreateGround(Scene simulationScene)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "BugCam Ground";
            ground.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            SceneManager.MoveGameObjectToScene(ground, simulationScene);
        }

        private static Rigidbody CreateBody(
            Scene simulationScene,
            SimulationBodyDefinition body,
            SimulationPerturbation perturbation)
        {
            var gameObject = new GameObject("BugCam Body");
            SceneManager.MoveGameObjectToScene(gameObject, simulationScene);
            var position = body.Position;
            if (perturbation.MagnitudeMetres != 0f &&
                body.StableId == perturbation.TargetBodyId)
            {
                position += perturbation.Axis * perturbation.MagnitudeMetres;
            }

            gameObject.transform.SetPositionAndRotation(position, body.Rotation);
            gameObject.transform.localScale = body.Size;
            if (body.Shape == SimulationColliderShape.Sphere)
            {
                // Unit sphere radius 0.5 × uniform localScale = Size ⇒ Size is the diameter.
                gameObject.AddComponent<SphereCollider>();
            }
            else
            {
                gameObject.AddComponent<BoxCollider>();
            }

            var rigidbody = gameObject.AddComponent<Rigidbody>();
            rigidbody.mass = body.Mass;
            rigidbody.isKinematic = false;
            rigidbody.useGravity = true;
            rigidbody.linearVelocity = body.InitialLinearVelocity;
            return rigidbody;
        }

    }
}

