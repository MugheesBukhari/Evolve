namespace GSS.Evolve
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using DG.Tweening;
    using System;

    public class BattleUnit : MonoBehaviour
    {
        public Data.UnitID id = Data.UnitID.mudclad;
        private Vector3 lastPosition = Vector3.zero;
        private int i = -1; public int index { get { return i; } }
        private long _id = 0; public long databaseID { get { return _id; } }
        [HideInInspector] public UI_Bar healthBar = null;
        [HideInInspector] public Data.Unit data = null;
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

        [Header("Animator")]
        [SerializeField] private Animator UnitAnimator;

        private bool _inCamp = false;
        private Building _camp = null;
        private Transform _barrack = null;
        private Transform _townHall = null;
        private Tween tween;
        private int waypointNo = 0;
        public int unitSpeed = 2;

        public Vector3[] pos;

        #region Defensive Base
        public void TweenCancel()
        {
            CancelInvoke("UnitIdle");
            if (tween != null)
            {
                tween.onComplete = null;
                tween.Kill();
            }
        }
        public void InitializeTrainedUnit(bool isPositionChanged = false)
        {
            TweenCancel();
            moving = true;
            if (!isPositionChanged)
                transform.position = UI_Main.instanse._grid.getBuildingPosition(Data.BuildingID.barracks).position;
            _camp = UI_Main.instanse._grid.getBuilding(Data.BuildingID.camp);
            Vector3 campPos = UI_Main.instanse._grid.getBuildingPosition(Data.BuildingID.camp).position;
            pos = GetPath(transform.position, campPos);
            tween = transform.DOPath(pos, unitSpeed, PathType.Linear).SetSpeedBased(true).SetEase(Ease.Linear).OnWaypointChange(WayPointReached)
            .OnComplete(() =>
            {
                moving = false;
                SetLookDirection(targetPosition);
                _camp = UI_Main.instanse._grid.getBuilding(Data.BuildingID.camp);
                Invoke("UnitIdle", 0.2f);
            });
        }
        public void AlreadyTrainedUnit()
        {
            transform.position = UI_Main.instanse._grid.getBuildingPosition(Data.BuildingID.camp).position;
            _camp = UI_Main.instanse._grid.getBuilding(Data.BuildingID.camp);
            Invoke("UnitIdle", 0.2f);
        }
        public void AttackOnBase()
        {
            TweenCancel();
            CancelInvoke("UnitIdle");
            Vector3 townHallPos = UI_Main.instanse._grid.getBuildingPosition(Data.BuildingID.commandcenter).position;
            pos = GetPath(transform.position, townHallPos);
            moving = true;
            attackonBase = true;
            tween = transform.DOPath(pos, unitSpeed, PathType.Linear).SetSpeedBased(true).SetEase(Ease.Linear).OnWaypointChange(WayPointReached)
           .OnComplete(() =>
           {
               moving = false;
               setUnitActive(false);
           });
        }
        private void setUnitActive(bool isActive)
        {
            if (baseTransform.gameObject.activeInHierarchy != isActive)
                baseTransform.gameObject.SetActive(isActive);
        }
        private void UnitIdle()
        {
            TweenCancel();
            if (!attackonBase && _camp)
            {
                //if (_camp.constructionPoints.Length > 1)
                //{
                //    positionTarget = _camp.constructionPoints[waypointNo].position;
                //    if (waypointNo == _camp.constructionPoints.Length - 1)
                //    {
                //        Array.Reverse(_camp.constructionPoints);
                //        waypointNo = 0;
                //    }
                //    waypointNo++;
                //}
                //else if (_camp.constructionPoints.Length == 1)
                //{
                //    positionTarget = _camp.constructionPoints[_camp.constructionPoints.Length - 1].position;
                //}
                //else
                //    positionTarget = _camp.transform.position;
                positionTarget = _camp.getCampPoint(transform.position);
                pos = MakePath(targetPosition);
                moving = true;
                tween = transform.DOPath(pos, unitSpeed, PathType.Linear).SetSpeedBased(true).SetEase(Ease.Linear).OnWaypointChange(WayPointReached)
                    .OnComplete(() =>
                    {
                        moving = false;
                        SetLookDirection(targetPosition);
                        //Invoke("UnitIdle", 5f);
                        Invoke("UnitIdle", UnityEngine.Random.Range(4, 8));
                    });
            }
        }
        private void WayPointReached(int waypoint)
        {
            if (waypoint < pos.Length)
            {
                Vector2Int v;
                UI_Main.instanse._grid.WorldToCell(pos[waypoint], out v);
                //Debug.Log("V is " + v);
                if (UI_Main.instanse._grid.pathFinder.grid.Grid[v.x, v.y].nodeState == NodeState.isJumpable)
                {
                    SetLookDirection(pos[waypoint], true);
                }
                else
                    SetLookDirection(pos[waypoint]);
            }
        }
        private Vector3[] GetPath(Vector3 startPos, Vector3 endPos)
        {
            Vector2Int characterPos;
            UI_Main.instanse._grid.WorldToCell(startPos, out characterPos);
            Vector2Int buildingPos;
            UI_Main.instanse._grid.WorldToCell(endPos, out buildingPos);
            List<Vector2Int> pathPos = UI_Main.instanse._grid.pathFinder.FindPath(characterPos, buildingPos);
            //Debug.Log("Path Pos Count" + pathPos.Count);
            Vector3[] finalPathPos = new Vector3[pathPos.Count + 1];
            for (int i = 0; i < pathPos.Count; i++)
            {
                finalPathPos[i] = UI_Main.instanse._grid.CellToWorld(pathPos[i].x, pathPos[i].y);
            }
            finalPathPos[finalPathPos.Length - 1] = _camp.getCampPoint(transform.position);
            return finalPathPos;
        }

        public Vector3[] MakePath(Vector3 targetPosition)
        {
            Vector3[] path = new Vector3[1];
            path[0] = targetPosition;
            return path;
        }

        #endregion



        #region Attack Base
        private void Awake()
        {
            if (moveEffect != null)
            {
                moveEffect.gameObject.SetActive(false);
            }
        }

        public void OnDeath()
        {
            PlayDeathSound();
        }

        private void OnDestroy()
        {
            TweenCancel();
            if (healthBar && healthBar != null)
            {
                Destroy(healthBar.gameObject);
            }
        }

        public void Initialize(int index, long id, Data.Unit unit)
        {
            positionOffset.x = UnityEngine.Random.Range(UI_Main.instanse._grid.cellSize * -0.45f, UI_Main.instanse._grid.cellSize * 0.45f);
            positionOffset.y = UnityEngine.Random.Range(UI_Main.instanse._grid.cellSize * -0.45f, UI_Main.instanse._grid.cellSize * 0.45f);
            data = unit;
            _id = id;
            i = index;
            lastPosition = transform.position;
            PlayAppearSound();
        }
        private void Update()
        {
            if (Player.inBattle || Data.isPVEAttack)
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
            Attack();
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
            //Debug.LogError("Animation is " + animationStatePrefix + " and direction is " + d);
            UnitAnimator.Play(animationStatePrefix + d);
            if (animationStatePrefix == "Unit_Attack_")
            {
                PlayAttackSound();
                PlayAttackParticle();
            }
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
        void PlayAppearSound()
        {
            switch (id)
            {
                case Data.UnitID.mudclad:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.MudcladDrop);
                    break;
                case Data.UnitID.scout:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.ScoutDrop);
                    break;
                case Data.UnitID.tiny:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.TinyDrop);
                    break;
            }
            Vibration.VibratePop();
        }
        void PlayAttackSound()
        {
            switch (id)
            {
                case Data.UnitID.mudclad:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.MudcladAttack);
                    break;
                case Data.UnitID.scout:
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.TinyAttack);
                    break;
            }
        }
        void PlayDeathSound()
        {
            SoundManager.instanse.PlaySound(SoundManager.SoundType.UnitDeath);
        }
        #endregion
    }
}