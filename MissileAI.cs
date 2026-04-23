using Unity.Netcode;
using UnityEngine;

namespace MissileTurret;

public class MissileAI : NetworkBehaviour
{
    public Transform player; // this jawng is null on client? but this shouldn't exist on client so idk man

    private float _speed = 0f;

    private float _currentLaunchTime;
    private readonly float launchTimeSeconds = 0.4f;

    private float _aliveTimeSeconds;
    
    private Rigidbody _rigidbody;

    public static float MaxTurnSpeed;
    public static float MaxSpeed;
    public static float Acceleration;

    public static float KillRange;
    public static float DamageRange;

    private float _isClientMult = 1f;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _currentLaunchTime = launchTimeSeconds;
    }

    private void Update()
    {
        if (!base.IsServer)
            return;

        _aliveTimeSeconds += Time.deltaTime;

        if (_currentLaunchTime > 0)
        {
            _currentLaunchTime -= Time.deltaTime;
            _speed += Acceleration * Time.deltaTime;
        }

        _speed = Mathf.Clamp(_speed, 0f, MaxSpeed);

        Vector3 forward = transform.forward;

        Vector3 newPosition = _rigidbody.position + forward * (_speed * Time.deltaTime);
        _rigidbody.MovePosition(newPosition);

        if (player != null)
        {
            Vector3 targetDir = (player.position + (Vector3.up/2) - transform.position).normalized;

            Quaternion targetRotation = Quaternion.LookRotation(targetDir);

            Quaternion newRotation = Quaternion.RotateTowards(
                _rigidbody.rotation,
                targetRotation,
                MaxTurnSpeed * Time.deltaTime
            );

            _rigidbody.MoveRotation(newRotation);
        }

        if (_aliveTimeSeconds > 10f)
            EndIt();
    }

    private void OnCollisionEnter(Collision other)
    {
        EndIt();
    }

    private void EndIt()
    {
        // only exists on the server anyway so this means nothing?
        if (!base.IsServer) return;

        ExplodeClientRpc(transform.position, KillRange, DamageRange);

        var net = GetComponent<NetworkObject>();
        
        if (net is not null && net.IsSpawned)
            net.Despawn();
        
        Destroy(gameObject);
    }
    
    [ClientRpc]
    public void ExplodeClientRpc(Vector3 position, float killRange, float damageRange)
    {
        if (!base.IsServer)
        {
            _isClientMult = 1.5f;
        } 
        // Slightly less forgiving for clients to account for lag
        Landmine.SpawnExplosion(position, true, (killRange * _isClientMult), damageRange);
    }

}