using UnityEngine;

public class HorizontalPaddleServer : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float xMin = -6.5f; // Limite esquerdo da tela
    public float xMax = 6.5f;  // Limite direito da tela

    public void MovePaddle(float direction)
    {
        Vector3 newPos = transform.position + Vector3.right * direction * moveSpeed * Time.deltaTime;
        
        // Trava a raquete horizontal dentro dos limites da arena
        newPos.x = Mathf.Clamp(newPos.x, xMin, xMax);
        transform.position = newPos;
    }
}