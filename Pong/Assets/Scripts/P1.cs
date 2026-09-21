using UnityEngine;

public class PaddleServer : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float yMin = -3.5f; // Limite inferior da tela
    public float yMax = 3.5f;  // Limite superior da tela

    // Função chamada pelo PongServer ao receber comandos dos clientes
    public void MovePaddle(float direction)
    {
        Vector3 newPos = transform.position + Vector3.up * direction * moveSpeed * Time.deltaTime;
        
        // Trava a raquete dentro da tela
        newPos.y = Mathf.Clamp(newPos.y, yMin, yMax);
        transform.position = newPos;
    }
}