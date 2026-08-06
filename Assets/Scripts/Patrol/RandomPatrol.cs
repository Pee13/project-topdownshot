using UnityEngine;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Patrol
{
    /// <summary>
    /// Randomly wanders within a defined area around a starting origin point.
    /// Used as a fallback when no waypoints are defined.
    /// </summary>
    public class RandomPatrol
    {
        private readonly Vector2 _origin;
        private readonly float _radius;
        private readonly LayerMask _obstacleMask;
        private Vector2 _currentTarget;

        public RandomPatrol(Vector2 origin, float radius, LayerMask obstacleMask)
        {
            _origin = origin;
            _radius = radius;
            _obstacleMask = obstacleMask;
            PickNewPoint();
        }

        public Vector2 CurrentTarget => _currentTarget;

        public void PickNewPoint()
        {
            if (PhysicsUtility.TryFindNearestValidPoint(_origin, _radius, _obstacleMask, out Vector2 point))
                _currentTarget = point;
            else
                _currentTarget = _origin;
        }

        public bool HasReached(Vector2 currentPosition, float threshold = 0.3f)
        {
            return Vector2.Distance(currentPosition, _currentTarget) <= threshold;
        }
    }
}
