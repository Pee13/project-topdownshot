using UnityEngine;
using System;

namespace TopDownTacticalAI.Player
{
    public enum EquipmentType { Shield, Weapon, Speed }

    public class PlayerEquipment : MonoBehaviour
    {
        [Header("Slots")]
        public Transform ShieldSlot;
        public Transform WeaponSlot;
        public Transform ThrusterSlot;

        [Header("Active Equipment")]
        public EquipmentInstance ActiveShield;
        public EquipmentInstance ActiveWeapon;
        public EquipmentInstance ActiveThruster;

        private PlayerController _playerController;
        private float _baseSpeed;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            if (_playerController != null) _baseSpeed = _playerController.MoveSpeed;
        }

        public void Equip(EquipmentType type, GameObject prefab, float maxDurability)
        {
            switch (type)
            {
                case EquipmentType.Shield:
                    SetupSlot(ref ActiveShield, ShieldSlot, prefab, maxDurability);
                    break;
                case EquipmentType.Weapon:
                    SetupSlot(ref ActiveWeapon, WeaponSlot, prefab, maxDurability);
                    break;
                case EquipmentType.Speed:
                    SetupSlot(ref ActiveThruster, ThrusterSlot, prefab, maxDurability);
                    if (_playerController != null) _playerController.MoveSpeed = _baseSpeed * 1.5f;
                    break;
            }
        }

        private void SetupSlot(ref EquipmentInstance slot, Transform parent, GameObject prefab, float durability)
        {
            if (slot.Visual != null) Destroy(slot.Visual);
            
            slot.Visual = Instantiate(prefab, parent);
            slot.Visual.transform.localPosition = Vector3.zero;
            slot.Visual.transform.localRotation = Quaternion.identity;
            slot.CurrentDurability = durability;
            slot.MaxDurability = durability;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateDurability(ref ActiveShield, dt, () => { });
            UpdateDurability(ref ActiveThruster, dt, () => { 
                if (_playerController != null) _playerController.MoveSpeed = _baseSpeed; 
            });

            // Dash Effect
            if (ActiveThruster.Visual != null && _playerController != null)
            {
                // Toggle thruster visibility or effect based on dashing
                bool shouldShowFire = _playerController.IsDashing;
                // Assuming the prefab has a "Fire" child or we just toggle the whole thing
                ActiveThruster.Visual.SetActive(shouldShowFire || ActiveThruster.CurrentDurability > 0);
                
                // If dashing, we might want to scale the "fire"
                if (_playerController.IsDashing)
                {
                    ActiveThruster.Visual.transform.localScale = Vector3.one * 1.5f;
                }
                else
                {
                    ActiveThruster.Visual.transform.localScale = Vector3.one;
                }
            }
        }

        private void UpdateDurability(ref EquipmentInstance instance, float amount, Action onBreak)
        {
            if (instance.Visual == null) return;

            instance.CurrentDurability -= amount;
            if (instance.CurrentDurability <= 0)
            {
                Destroy(instance.Visual);
                instance.Visual = null;
                onBreak?.Invoke();
            }
        }

        public void UseWeaponCharge(float amount = 1f)
        {
            UpdateDurability(ref ActiveWeapon, amount, () => { });
        }
    }

    [Serializable]
    public struct EquipmentInstance
    {
        public GameObject Visual;
        public float CurrentDurability;
        public float MaxDurability;
    }
}
