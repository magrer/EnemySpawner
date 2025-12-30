using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("Настройки спавна")]
    public GameObject enemyPrefab;
    public Transform[] spawnPoints;
    public float spawnRadius = 3f;
    
    [Header("Волны")]
    public int currentWave = 0;
    public int enemiesPerWave = 3;
    public float waveCooldown = 5f;
    public float timeBetweenSpawns = 1f;
    
    [Header("Рандомизация врагов")]
    public float minSize = 0.5f;
    public float maxSize = 2f;
    public int minHealth = 10;
    public int maxHealth = 30;
    
    [Header("Эскалация")]
    public float waveMultiplier = 1.2f;
    public float sizeMultiplier = 1.05f;
    public float healthMultiplier = 1.1f;
    
    private List<GameObject> activeEnemies = new List<GameObject>();
    private bool waveInProgress = false;
    private bool waitingForNextWave = false;
    
    void Start()
    {
        StartNextWave();
    }
    
    void Update()
    {
        // Если волна идет, проверяем живых врагов
        if (waveInProgress)
        {
            // Удаляем уничтоженных врагов из списка
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                if (activeEnemies[i] == null)
                {
                    activeEnemies.RemoveAt(i);
                }
            }
            
            // Если все враги убиты и мы еще не начали отсчет до следующей волны
            if (activeEnemies.Count == 0 && waveInProgress && !waitingForNextWave)
            {
                waveInProgress = false;
                waitingForNextWave = true;
                Debug.Log($"Волна {currentWave} завершена! Следующая волна через {waveCooldown} сек.");
                
                // Запускаем следующую волну через паузу
                Invoke("StartNextWave", waveCooldown);
            }
        }
    }
    
    void StartNextWave()
    {
        waitingForNextWave = false;
        StartCoroutine(SpawnWave());
    }
    
    System.Collections.IEnumerator SpawnWave()
    {
        currentWave++;
        waveInProgress = true;
        
        // Увеличиваем сложность
        int enemiesThisWave = Mathf.RoundToInt(enemiesPerWave * Mathf.Pow(waveMultiplier, currentWave - 1));
        float currentMaxSize = maxSize * Mathf.Pow(sizeMultiplier, currentWave - 1);
        int currentMaxHealth = Mathf.RoundToInt(maxHealth * Mathf.Pow(healthMultiplier, currentWave - 1));
        
        Debug.Log($"=== ВОЛНА {currentWave} ===");
        Debug.Log($"Врагов: {enemiesThisWave}");
        Debug.Log($"Макс. размер: {currentMaxSize:F1}");
        Debug.Log($"Макс. здоровье: {currentMaxHealth}");
        
        for (int i = 0; i < enemiesThisWave; i++)
        {
            SpawnEnemy(currentMaxSize, currentMaxHealth);
            yield return new WaitForSeconds(timeBetweenSpawns);
        }
        
        Debug.Log($"Все вражи волны {currentWave} заспавнены. Ожидаем их уничтожения...");
    }
    
    void SpawnEnemy(float currentMaxSize, int currentMaxHealth)
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogError("Нет точек спавна!");
            return;
        }
        
        if (enemyPrefab == null)
        {
            Debug.LogError("Нет префаба врага!");
            return;
        }
        
        // Выбираем случайную точку спавна
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        
        // Случайная позиция вокруг точки
        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnRadius, spawnRadius),
            0,
            Random.Range(-spawnRadius, spawnRadius)
        );
        
        Vector3 spawnPosition = spawnPoint.position + randomOffset;
        
        // Проверяем, что точка на NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPosition, out hit, 5f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }
        
        // Создаем врага
        GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        activeEnemies.Add(enemy);
        
        // Инициализируем врага
        InitializeEnemy(enemy, currentMaxSize, currentMaxHealth);
        
        Debug.Log($"Создан враг #{activeEnemies.Count} волны {currentWave} в позиции {spawnPosition}");
    }
    
    void InitializeEnemy(GameObject enemy, float currentMaxSize, int currentMaxHealth)
    {
        if (enemy == null) return;
        
        try
        {
            // 1. Рандомный размер
            float randomSize = Random.Range(minSize, currentMaxSize);
            enemy.transform.localScale = Vector3.one * randomSize;
            
            // 2. Цвет
            float sizePercent = randomSize / currentMaxSize;
            Color enemyColor = Color.Lerp(Color.yellow, Color.red, sizePercent);
            
            MeshRenderer renderer = enemy.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = enemyColor;
            }
            
            // 3. Здоровье
            EnemyHealth health = enemy.GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.maxHealth = Mathf.RoundToInt(minHealth + (currentMaxHealth - minHealth) * sizePercent);
                
                // Вызываем Start если он есть
                health.SendMessage("ResetHealth", SendMessageOptions.DontRequireReceiver);
            }
            
            // 4. Скорость движения
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
                agent.speed = 3.5f - sizePercent;
                agent.stoppingDistance = 2f;
                agent.angularSpeed = 360f;
                agent.acceleration = 8f;
                
                // Важно: активируем поиск пути
                agent.isStopped = false;
            }
            
            // 5. Включаем AI
            SimpleEnemyAI ai = enemy.GetComponent<SimpleEnemyAI>();
            if (ai != null)
            {
                ai.enabled = true;
                ai.attackDamage = Mathf.RoundToInt(10 * sizePercent);
                
                // Переинициализируем AI
                ai.SendMessage("Initialize", SendMessageOptions.DontRequireReceiver);
            }
            
            // 6. Сбрасываем физику
            Rigidbody rb = enemy.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при инициализации врага: {e.Message}");
        }
    }
    
    // Метод для отладки - отображает информацию в окне Game
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        
        GUI.Label(new Rect(10, 10, 300, 30), $"Волна: {currentWave}", style);
        GUI.Label(new Rect(10, 40, 300, 30), $"Врагов осталось: {activeEnemies.Count}", style);
        GUI.Label(new Rect(10, 70, 300, 30), $"Статус: {(waveInProgress ? "Волна идет" : (waitingForNextWave ? "Ожидание" : "Пауза"))}", style);
    }
}