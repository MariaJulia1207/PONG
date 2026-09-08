using UnityEngine;

public class P1 : MonoBehaviour
{
    public float moveSpeed;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        bool isPressingUp = Input.GetKey(KeyCode.W);
        bool isPressingDown = Input.GetKey(KeyCode.S);

        if (isPressingUp)
        {
            transform.Translate(Vector3.up * Time.deltaTime * moveSpeed);
        }
        if (isPressingDown)
        {
            transform.Translate(Vector3.down * Time.deltaTime  * moveSpeed);
        }
    }
}
