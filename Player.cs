using UnityEngine;
using Fusion;
using Cinemachine;

namespace SimpleFPS
{
	[DefaultExecutionOrder(-5)]
	public class Player : NetworkBehaviour
	{
		[Header("Components")]
		public CapsuleCollider CapsuleCollider;
		public Weapons Weapons;
		public Health Health;
		public Animator Animator;
		public HitboxRoot HitboxRoot;

		[Header("Setup")]
		public float MoveSpeed = 6f;
		public float JumpForce = 10f;
		public AudioSource JumpSound;
		public AudioSource FootSound;
		public AudioSource switchSound;
		public AudioClip[] JumpClips;
		public Transform CameraHandle;
		public GameObject FirstPersonRoot;
		public GameObject ThirdPersonRoot;
		public NetworkObject SprayPrefab;

		[Header("Movement")]
		public float UpGravity = 15f;
		public float DownGravity = 25f;
		public float GroundAcceleration = 55f;
		public float GroundDeceleration = 25f;
		public float AirAcceleration = 25f;
		public float AirDeceleration = 1.3f;

		[Header("Custom Controller")]
		public LayerMask CollisionMask = ~0;
		public float SkinWidth = 0.03f;
		public float GroundCheckDistance = 0.2f;

		[Networked] protected NetworkButtons _previousButtons { get; set; }
		[Networked] protected int _jumpCount { get; set; }
		[Networked] protected Vector3 _moveVelocity { get; set; }
		[Networked] protected float _verticalVelocity { get; set; }
		[Networked] protected float _pitch { get; set; }
		[Networked] protected NetworkBool _isGrounded { get; set; }

		public bool IsGrounded => _isGrounded;
		protected int _visibleJumpCount;
		protected SceneObjects _sceneObjects;

		public virtual void PlayFireEffect()
		{
			if (Mathf.Abs(GetAnimationMoveVelocity().x) > 0.2f)
				return;
			Animator.SetTrigger("Fire");
		}

		public override void Spawned()
		{
			name = $"{Object.InputAuthority} ({(HasInputAuthority ? "Input Authority" : (HasStateAuthority ? "State Authority" : "Proxy"))})";
			SetFirstPersonVisuals(HasInputAuthority);

			if (!HasInputAuthority)
			{
				var virtualCameras = GetComponentsInChildren<CinemachineVirtualCamera>(true);
				for (int i = 0; i < virtualCameras.Length; i++)
					virtualCameras[i].enabled = false;
			}

			if (CapsuleCollider == null)
				CapsuleCollider = GetComponent<CapsuleCollider>();

			_sceneObjects = Runner.GetSingleton<SceneObjects>();
		}

		public override void FixedUpdateNetwork()
		{
			if (_sceneObjects.Gameplay.State == EGameplayState.Finished)
			{
				MovePlayer();
				return;
			}

			if (!Health.IsAlive)
			{
				MovePlayer();
				HitboxRoot.HitboxRootActive = false;
				SetFirstPersonVisuals(false);
				return;
			}

			if (GetInput(out NetworkedInput input))
				ProcessInput(input);
			else
			{
				MovePlayer();
				RefreshCamera();
			}
		}

		public override void Render()
		{
			if (_sceneObjects.Gameplay.State == EGameplayState.Finished)
				return;

			var moveVelocity = GetAnimationMoveVelocity();
			Animator.SetFloat("LocomotionTime", Time.time * 2f);
			Animator.SetBool("IsAlive", Health.IsAlive);
			Animator.SetBool("IsGrounded", IsGrounded);
			Animator.SetBool("IsReloading", Weapons.CurrentWeapon.IsReloading);
			Animator.SetFloat("MoveX", moveVelocity.x, 0.05f, Time.deltaTime);
			Animator.SetFloat("MoveZ", moveVelocity.z, 0.05f, Time.deltaTime);
			Animator.SetFloat("MoveSpeed", moveVelocity.magnitude);
			Animator.SetFloat("Look", -_pitch / 90f);

			if (!Health.IsAlive)
			{
				int upperBodyLayerIndex = Animator.GetLayerIndex("UpperBody");
				Animator.SetLayerWeight(upperBodyLayerIndex, Mathf.Max(0f, Animator.GetLayerWeight(upperBodyLayerIndex) - Time.deltaTime));
				int lookLayerIndex = Animator.GetLayerIndex("Look");
				Animator.SetLayerWeight(lookLayerIndex, Mathf.Max(0f, Animator.GetLayerWeight(lookLayerIndex) - Time.deltaTime));
			}

			if (_visibleJumpCount < _jumpCount)
			{
				Animator.SetTrigger("Jump");
				JumpSound.clip = JumpClips[Random.Range(0, JumpClips.Length)];
				JumpSound.Play();
			}

			_visibleJumpCount = _jumpCount;
		}

		protected virtual void LateUpdate()
		{
			if (!HasInputAuthority)
				return;
			RefreshCamera();
		}

		protected virtual void ProcessInput(NetworkedInput input)
		{
			ApplyLookRotation(input.LookRotationDelta, -89f, 89f);

			var inputDirection = transform.rotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
			var jumpImpulse = 0f;

			if (input.Buttons.WasPressed(_previousButtons, EInputButton.Jump) && IsGrounded)
				jumpImpulse = JumpForce;

			if (input.Buttons.WasPressed(_previousButtons, EInputButton.ResetGyro))
				ResetGyro();

			MovePlayer(inputDirection * MoveSpeed, jumpImpulse);
			RefreshCamera();

			if (jumpImpulse > 0f)
				_jumpCount++;

			if (input.Buttons.IsSet(EInputButton.Fire))
			{
				bool justPressed = input.Buttons.WasPressed(_previousButtons, EInputButton.Fire);
				Weapons.Fire(justPressed);
				Health.StopImmortality();
			}
			else if (input.Buttons.IsSet(EInputButton.Reload))
			{
				Weapons.Reload();
			}

			if (input.Buttons.WasPressed(_previousButtons, EInputButton.Pistol))
				Weapons.SwitchWeapon(EWeaponType.Pistol);
			else if (input.Buttons.WasPressed(_previousButtons, EInputButton.Rifle))
				Weapons.SwitchWeapon(EWeaponType.Rifle);
			else if (input.Buttons.WasPressed(_previousButtons, EInputButton.Shotgun))
				Weapons.SwitchWeapon(EWeaponType.Shotgun);

			if (input.Buttons.WasPressed(_previousButtons, EInputButton.Spray) && HasStateAuthority)
			{
				if (Runner.GetPhysicsScene().Raycast(CameraHandle.position, CameraHandle.forward, out var hit, 2.5f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
				{
					var sprayOrientation = hit.normal.y > 0.9f ? transform.rotation : Quaternion.identity;
					Runner.Spawn(SprayPrefab, hit.point, sprayOrientation * Quaternion.LookRotation(-hit.normal));
				}
			}

			_previousButtons = input.Buttons;
		}

		protected virtual void MovePlayer(Vector3 desiredMoveVelocity = default, float jumpImpulse = default)
		{
			bool groundedAtStart = CheckGrounded();

			// 🎯 CONSTANT Z SPEED (no acceleration ever)
			float zSpeed = Mathf.Sign(desiredMoveVelocity.z) * MoveSpeed;
			if (desiredMoveVelocity.z == 0)
				zSpeed = 0f;

			Vector3 horizontalVelocity = new Vector3(0f, 0f, zSpeed);

			// 🦘 Jump
			if (groundedAtStart)
			{
				if (_verticalVelocity < 0f)
					_verticalVelocity = 0f;

				if (jumpImpulse > 0f)
					_verticalVelocity = jumpImpulse;
			}

			// 🌍 Gravity (only affects Y)
			_verticalVelocity += -DownGravity * Runner.DeltaTime;

			Vector3 verticalVelocity = Vector3.up * _verticalVelocity;

			// 🚀 Final displacement (separate horizontal & vertical)
			Vector3 displacement = (horizontalVelocity + verticalVelocity) * Runner.DeltaTime;

			ApplyCustomControllerMove(displacement);

			// 🔒 HARD CLAMP Z
			Vector3 pos = transform.position;
			pos.z = Mathf.Clamp(pos.z, -1.5f, 3.5f);
			transform.position = pos;

			_isGrounded = CheckGrounded();
		}



		//protected virtual void MovePlayer(Vector3 desiredMoveVelocity = default, float jumpImpulse = default)
		//{
		//	bool groundedAtStart = CheckGrounded();
		//	float acceleration = desiredMoveVelocity == Vector3.zero
		//		? (groundedAtStart ? GroundDeceleration : AirDeceleration)
		//		: (groundedAtStart ? GroundAcceleration : AirAcceleration);

		//	_moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);

		//	if (groundedAtStart && _verticalVelocity < 0f)
		//		_verticalVelocity = -2f;

		//	if (jumpImpulse > 0f && groundedAtStart)
		//		_verticalVelocity = jumpImpulse;

		//	float gravity = _verticalVelocity >= 0f ? -UpGravity : -DownGravity;
		//	_verticalVelocity += gravity * Runner.DeltaTime;

		//	Vector3 displacement = (_moveVelocity * _verticalVelocity) * Runner.DeltaTime;
		//	ApplyCustomControllerMove(displacement);
		//	_isGrounded = CheckGrounded();
		//}

		private void ApplyCustomControllerMove(Vector3 displacement)
        {
            if (displacement == Vector3.zero)
                return;

            Vector3 position = transform.position;
            Vector3 horizontal = new Vector3(displacement.x, 0f, displacement.z);
            Vector3 vertical = new Vector3(0f, displacement.y, 0f);

            position = ResolveCollision(position, horizontal, false);
            position = ResolveCollision(position, vertical, true);

            transform.position = position;
        }

        private Vector3 ResolveCollision(Vector3 origin, Vector3 delta, bool verticalPass)
		{
			if (delta.sqrMagnitude <= 0f)
				return origin;

			Vector3 direction = delta.normalized;
			float distance = delta.magnitude;
			GetCapsulePoints(origin, out var p1, out var p2, out float radius);

			if (Physics.CapsuleCast(p1, p2, radius, direction, out RaycastHit hit, distance + SkinWidth, CollisionMask, QueryTriggerInteraction.Ignore))
			{
				float allowed = Mathf.Max(0f, hit.distance - SkinWidth);
				Vector3 moved = origin + direction * allowed;

				if (!verticalPass)
				{
					Vector3 slide = Vector3.ProjectOnPlane(delta - direction * allowed, hit.normal);
					moved += slide * 0.5f;
				}
				else if (delta.y < 0f)
				{
					_verticalVelocity = 0f;
				}

				return moved;
			}

			return origin + delta;
		}

		private bool CheckGrounded()
		{
			GetCapsulePoints(transform.position, out var p1, out var p2, out float radius);
			float checkDistance = GroundCheckDistance + SkinWidth;
			return Physics.SphereCast(p1, radius * 0.95f, Vector3.down, out _, checkDistance, CollisionMask, QueryTriggerInteraction.Ignore);
		}

		private void GetCapsulePoints(Vector3 position, out Vector3 point1, out Vector3 point2, out float radius)
		{
			float height = 2f;
			radius = 0.4f;
			Vector3 center = Vector3.up;

			if (CapsuleCollider != null)
			{
				height = CapsuleCollider.height * transform.localScale.y;
				radius = CapsuleCollider.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
				center = transform.TransformVector(CapsuleCollider.center);
			}

			float half = Mathf.Max(radius, height * 0.5f - radius);
			Vector3 worldCenter = position + center;
			point1 = worldCenter + Vector3.up * half;
			point2 = worldCenter - Vector3.up * half;
		}

		protected virtual void RefreshCamera()
		{
			CameraHandle.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
		}
		[Networked] protected float _yaw { get; set; }
		protected virtual void ApplyLookRotation(Vector2 lookDelta, float minPitch, float maxPitch)
		{
			_pitch = Mathf.Clamp(_pitch - lookDelta.x, minPitch, maxPitch);
			Debug.Log("lookDelta.y== " + lookDelta.y);
			//transform.Rotate(0f, lookDelta.y, 0f);

			//CameraHandle.localRotation = Quaternion.Euler(lookDelta);
			// 🎯 Yaw (player left/right)
			_yaw += lookDelta.y;

			// Apply yaw rotation
			transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
			Debug.Log("transform.Rotate== " + transform.rotation.y);
		}

		public void ResetGyro()
		{
			if (!Object.HasStateAuthority)
			{
				RPC_RequestGyroReset();
				return;
			}
			ApplyGyroReset(Object.HasInputAuthority ? 90f : -90f);
		}

		[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
		private void RPC_RequestGyroReset()
		{
			ApplyGyroReset(-90f);
		}

		private void ApplyGyroReset(float yaw)
		{
			_pitch = 0f;
			_yaw = yaw;
			CameraHandle.localRotation = Quaternion.identity;
			transform.rotation = Quaternion.Euler(0f, yaw, 0f);
			RefreshCamera();
		}

		protected virtual void SetFirstPersonVisuals(bool firstPerson)
		{
			FirstPersonRoot.SetActive(firstPerson);
			ThirdPersonRoot.SetActive(!firstPerson);
		}

		protected virtual Vector3 GetAnimationMoveVelocity()
		{
			var velocity = _moveVelocity;
			if (velocity.sqrMagnitude < 0.0001f)
				return default;

			velocity.y = 0f;
			if (velocity.sqrMagnitude > 1f)
				velocity.Normalize();

			return transform.InverseTransformVector(velocity);
		}
	}
}