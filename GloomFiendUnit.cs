namespace GSS.Evolve
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using DG.Tweening;
    using System;

    public class GloomFiendUnit : MonoBehaviour
    {
        public Data.GloomFiendID id = Data.GloomFiendID.funguls;
        private Vector3 lastPosition = Vector3.zero;
        private int i = -1; public int index { get { return i; } }
        private long _id = 0; public long databaseID { get { return _id; } }
        [HideInInspector] public UI_Bar healthBar = null;
        [HideInInspector] public Data.GloomFiend data = null;
        [HideInInspector] public Vector3 positionOffset = Vector3.zero;
        [HideInInspector] public Vector3 positionTarget = Vector3.zero;
        [HideInInspector] public Vector3 targetPosition { get { return positionTarget + positionOffset; } }
        [HideInInspector] public bool moving = false;
        [HideInInspector] public bool attack = false;
        [HideInInspector] public bool attackonBase = false;

        [Header("Weapon")]
        public UI_Projectile projectilePrefab = null;
        public GameObject hitPrefab = null;
        public Transform targetPoint = null;
        public Transform shootPoint = null;

        [Header("Movement")]
        [SerializeField] private Transform baseTransform = null;
        [SerializeField] private ParticleSystem moveEffect = null;
        [SerializeField] private ParticleSystem transitionEffect = null;
        public bool isCampaignGloomfiend = false;

        [Header("Animator")]
        [SerializeField] private Animator UnitAnimator;

        private bool _inCamp = false;
        private Building _camp = null;
        private Transform _barrack = null;
        private Transform _townHall = null;
        private Tween tween;
        private int waypointNo = 0;
        public float moveSpeed = 1;
        public float attackSpeed = 1;
        public GameObject _deathParticles;
        public Vector3[] pos;

        #region Attack Base
        private void Awake()
        {
            if (moveEffect != null)
            {
                moveEffect.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (healthBar && healthBar != null)
            {
                Destroy(healthBar.gameObject);
            }
        }

        public void OnDeath()
        {
            if (_deathParticles)
            {
                Vector3 pos = transform.position;
                pos.y += 0.5f;
                PlayDeathSound();
                Instantiate(_deathParticles, pos, Quaternion.identity);
            }
        }

        public void Initialize(int index, long id, Data.GloomFiend gloomfiendUnit,bool isCampaignGrid = false)
        {
            positionOffset.x = UnityEngine.Random.Range(UI_Main.instanse._grid.cellSize * -0.45f, UI_Main.instanse._grid.cellSize * 0.45f);
            positionOffset.y = UnityEngine.Random.Range(UI_Main.instanse._grid.cellSize * -0.45f, UI_Main.instanse._grid.cellSize * 0.45f);
            data = gloomfiendUnit;
            _id = id;
            i = index;
            moveSpeed = gloomfiendUnit.moveSpeed;
            attackSpeed = gloomfiendUnit.attackSpeed;
            PlayAppearSound();
            lastPosition = transform.position;
        }
        private void Update()
        {
            if (Player.inPVEDefense || Data.isPVEAttack)
            {
                bool prevMoving = moving;
                if (transitionEffect != null && moving != prevMoving)
                {
                    transitionEffect.Play();
                }
                if (transform.position != targetPosition)
                {
                    SetLookDirection(targetPosition);
                    transform.position = Vector3.Lerp(transform.position, targetPosition, 10f * Time.deltaTime);
                }
                if (moveEffect != null)
                {
                    moveEffect.gameObject.SetActive(moving);
                    if (baseTransform != null)
                    {
                        baseTransform.gameObject.SetActive(!moving);
                    }
                }
                //if (moving)
                //{
                //    if (id != Data.UnitID.healer && id != Data.UnitID.dragon && id != Data.UnitID.babydragon && id != Data.UnitID.balloon)
                //    {
                //        // Play Move Animation
                //    }
                //    else
                //    {
                //        // Play Fly Animation
                //    }
                //}
                //else
                //{
                //    // Play Idle Animation
                //}
                lastPosition = transform.position;
            }
        }

        public void Attack(Vector3 position)
        {
            moving = false;
            attack = true;
            transform.position = targetPosition;
            SetLookDirection(position);
        }

        public void Attack()
        {
            attack = true;
        }
        #endregion
        private void SetLookDirection(Vector3 target, bool isJump = false)
        {
            Vector3 direction = target - transform.position;
            Direction d; // Default direction

            // Determine the primary direction based on the angle.
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            if (angle < 0) angle += 360; // Ensure the angle is always positive

            if (angle >= 337.5 || angle < 22.5)
            {
                d = Direction.Right;
            }
            else if (angle >= 22.5 && angle < 67.5)
            {
                d = Direction.UpRight;
            }
            else if (angle >= 67.5 && angle < 112.5)
            {
                d = Direction.Up;
            }
            else if (angle >= 112.5 && angle < 157.5)
            {
                d = Direction.UpLeft;
            }
            else if (angle >= 157.5 && angle < 202.5)
            {
                d = Direction.Left;
            }
            else if (angle >= 202.5 && angle < 247.5)
            {
                d = Direction.DownLeft;
            }
            else if (angle >= 247.5 && angle < 292.5)
            {
                d = Direction.Down;
            }
            else // angle >= 292.5 && angle < 337.5
            {
                d = Direction.DownRight;
            }

            // Determine action based on whether character is jumping or moving.
            string animationStatePrefix = GetAnimationStatePrefix(isJump);
            if (animationStatePrefix == "Unit_Attack_")
            {
                UnitAnimator.speed = attackSpeed;
                UnitAnimator.Play(animationStatePrefix + d, -1, 0f);
                PlayAttackSound();
                PlayAttackParticle();
            }
            else
            {
                UnitAnimator.speed = moveSpeed;
                UnitAnimator.Play(animationStatePrefix + d);
            }

            //Debug.LogError("Animation is " + animationStatePrefix + " and direction is " + d);
            attack = false;
        }
        private string GetAnimationStatePrefix(bool isJump)
        {
            if (attack)
                return "Unit_Attack_";
            if (isJump)
                return "Unit_Jump_"; // Prefix for jumping animations
            else if (moving)
                return "Unit_Move_"; // Prefix for moving animations
            return "Unit_Idle_"; // Default prefix when the character is not moving or jumping
        }
        void PlayAttackParticle()
        {
            if (hitPrefab)
                Instantiate(hitPrefab, transform.position, Quaternion.identity);
        }
        #region Sounds
        void PlayDeathSound()
        {
            switch (id)
            {
                case Data.GloomFiendID.funguls:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.FungulsDeath);
                    break;
                case Data.GloomFiendID.drebbler:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.DrebblerDeath);
                    break;
                case Data.GloomFiendID.prowler:
                case Data.GloomFiendID.boss:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.ProwlerDeath);
                    break;
            }
        }
        void PlayAttackSound()
        {
            switch (id)
            {
                case Data.GloomFiendID.funguls:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.FungulsAttack);
                    break;
                case Data.GloomFiendID.drebbler:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.DrebblerFire);
                    break;
                case Data.GloomFiendID.prowler:
                case Data.GloomFiendID.boss:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.ProwlerAttack);
                    break;
            }
        }
        void PlayAppearSound()
        {
            switch (id)
            {
                case Data.GloomFiendID.funguls:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.FungulsAttack);
                    break;
                case Data.GloomFiendID.drebbler:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.DrebblerFire);
                    break;
                case Data.GloomFiendID.prowler:
                case Data.GloomFiendID.boss:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.ProwlerAttack);
                    break;
            }
        }
        #endregion
    }
}