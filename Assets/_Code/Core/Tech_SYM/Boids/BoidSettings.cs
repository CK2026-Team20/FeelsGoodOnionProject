using UnityEngine;
using Cooked.Authoring;

namespace Cooked.Boids
{
    [CreateAssetMenu(menuName = "Cooked/Boids/군집 설정")]
    public sealed class BoidSettings : ScriptableObject
    {
        [SerializeField, InspectorLabel("최소 속력"), Tooltip("계속 이동하기 위한 최소 속력입니다(m/s)."), Min(.1f)]
        float minimumSpeed = 2.5f;
        [SerializeField, InspectorLabel("기준 속력"), Tooltip("최초 이동 속력입니다(m/s)."), Min(.1f)]
        float cruiseSpeed = 4;
        [SerializeField, InspectorLabel("최대 속력"), Tooltip("정렬 후 허용하는 최대 속력입니다(m/s)."), Min(.1f)]
        float maximumSpeed = 6;
        [SerializeField, InspectorLabel("분리 거리"), Tooltip("이웃과 간격을 벌리는 거리입니다(m)."), Min(.1f)]
        float separationRadius = 1;
        [SerializeField, InspectorLabel("이웃 거리"), Tooltip("각 개체를 중심으로 같은 군집의 이웃을 찾는 반경입니다(m). 군집 기준점으로부터의 최대 거리가 아닙니다."), Min(.1f)]
        float neighborRadius = 3;
        [SerializeField, InspectorLabel("분리 가중치"), Tooltip("가까운 개체와 떨어지려는 기여입니다."), Min(0)]
        float separationWeight = 2;
        [SerializeField, InspectorLabel("정렬 가중치"), Tooltip("이웃의 방향과 속력에 맞추는 기여입니다."), Min(0)]
        float alignmentWeight = 1;
        [SerializeField, InspectorLabel("응집 가중치"), Tooltip("이웃 중심에 접근하는 기여입니다. 중심 근처에서는 줄어듭니다."), Min(0)]
        float cohesionWeight = .5f;
        [SerializeField, InspectorLabel("반응 시간"), Tooltip("평상시 방향과 속력의 변화를 부드럽게 적용하는 시간입니다(s). 회피 가속도는 별도로 자동 계산합니다."), Min(.05f)]
        float responseTime = .35f;
        [SerializeField, InspectorLabel("예측 시간"), Tooltip("움직이는 장애물과 경계까지 미리 확인하는 시간입니다(s)."), Range(.3f, 3)]
        float predictionTime = 1.2f;
        [SerializeField, InspectorLabel("안전 간격"), Tooltip("개체 몸 반경에 더하는 회피 여유입니다(m)."), Min(0)]
        float clearance = .08f;
        [SerializeField, InspectorLabel("후보 방향 수"), Tooltip("황금비 구면의 회피 후보 개수입니다."), Range(16, 128)]
        int directionCount = 48;
        public float MinimumSpeed => minimumSpeed;
        public float CruiseSpeed => cruiseSpeed;
        public float MaximumSpeed => maximumSpeed;
        public float SeparationRadius => separationRadius;
        public float NeighborRadius => neighborRadius;
        public float SeparationWeight => separationWeight;
        public float AlignmentWeight => alignmentWeight;
        public float CohesionWeight => cohesionWeight;
        public float ResponseTime => responseTime;
        public float PredictionTime => predictionTime;
        public float Clearance => clearance;
        public int DirectionCount => directionCount;
        public void Validate()
        {
            foreach (float value in new[]{minimumSpeed,
                 cruiseSpeed,
                 maximumSpeed,
                 separationRadius,
                 neighborRadius,
                 separationWeight,
                 alignmentWeight,
                 cohesionWeight,
                 responseTime,
                 predictionTime,
                 clearance})
                if (!float.IsFinite(value))
                    throw new System.InvalidOperationException("군집 설정은 유한값이어야 합니다.");
            if (separationWeight < 0 || alignmentWeight < 0 || cohesionWeight < 0 || clearance < 0)
                throw new System.InvalidOperationException("군집 가중치와 안전 간격은 음수일 수 없습니다.");
            if (!float.IsFinite(minimumSpeed)
                || minimumSpeed <= 0
                || cruiseSpeed < minimumSpeed
                || maximumSpeed < cruiseSpeed
                || neighborRadius < separationRadius
                || separationRadius <= 0
                || responseTime <= 0
                || predictionTime <= 0
                || directionCount < 16)
                throw new System.InvalidOperationException("보이드 설정의 속력·거리·시간 범위를 확인하세요.");
        }
    }
}
