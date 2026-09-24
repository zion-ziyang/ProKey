using UnityEngine;

// Attached to the Trigger child object of each key
public class DwellKeyTrigger : MonoBehaviour
{
    private DwellKeyBehaviour keyBehaviour;
    public KeyZoneType zoneType;

    void Start()
    {
        if (transform.parent != null)
        {
            keyBehaviour = transform.parent.GetComponentInChildren<DwellKeyBehaviour>();
        }
        
        // Check if successfully found
        if (keyBehaviour == null)
        {
            Debug.LogError($"KeyTrigger cannot find KeyBehaviour script in child objects of {transform.parent.name}! Please check the Prefab structure.", this.gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("trigger")) // Assuming the finger Tag is "trigger"
        {
            keyBehaviour?.OnFingerEnterZone(zoneType);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("trigger"))
        {
            keyBehaviour?.OnFingerLeaveZone(zoneType);
        }
    }
}