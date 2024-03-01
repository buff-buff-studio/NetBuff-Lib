using NetBuff.Components;

namespace ExamplePlatformer
{
    public class LevelManager : NetworkBehaviour
    {
        private static LevelManager _instance;
        
        public OrbitCamera[] orbitCameras;
        
        public static LevelManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<LevelManager>();
                return _instance;
            }
            private set => _instance = value;
        }
        
        public int levelIndex;
        public NetworkIdentity[] levels;
        
        
        public override void OnSpawned(bool isRetroactive)
        {
            if (!HasAuthority) 
                return;
            
            Instance = this;
            levels[levelIndex].SetActive(true);
        }
        
        public void ChangeLevel(int level)
        {
            if (!HasAuthority)
                return;
            levels[levelIndex].SetActive(false);
            levelIndex = level;
            levels[levelIndex].SetActive(true);
        }
    }
}