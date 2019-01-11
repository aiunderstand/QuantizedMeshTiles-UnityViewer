using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ET_MapCameraController : MonoBehaviour
{

    public float maxZoom = 1000;
    public float minZoom = 100;

    public float scrollSpeed = 25;
    public float panSpeed = 500;
    public float zoomSpeed = 20;
    public float pinchZoomFactor = 0.5f;

    public GameObject test;
    public float zoomLevel = 500;

    Camera camera;

    private Vector3 lastPoint;
    private bool isZooming = false;
    private Vector3 zoomTarget;



    private void Awake()
    {
        camera = GetComponent<Camera>();
    }
    // Update is called once per frame
    void Update()
    {
        //==== Rotation with middle mouse button ====
        if (Input.GetMouseButton(2))
        {
            float mouseInput = Input.GetAxis("Mouse X");
            Vector3 lookhere = new Vector3(0, 0, mouseInput);
            transform.Rotate(lookhere);
        }

        //==== Panning the camera with WASD
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
        {
            var deltaX = Input.GetAxisRaw("Horizontal") * transform.right;
            var deltaZ = Input.GetAxisRaw("Vertical") * transform.forward;

            var offset = Vector3.Normalize(deltaX + deltaZ);
            transform.localPosition += offset * scrollSpeed * Time.deltaTime;
        }




        //==== Panning the camera 
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            var ba = EventSystem.current.IsPointerOverGameObject();
            lastPoint = GetCursorWorldPoint();
        }



        if (Input.touchCount == 2)
        {
            Touch FirstTouch = Input.GetTouch(0);
            Touch SecondTouch = Input.GetTouch(1);
            Vector2 touchPrevPos1 = FirstTouch.position - FirstTouch.deltaPosition;
            Vector2 touchPrevPos2 = SecondTouch.position - SecondTouch.deltaPosition;

            float prevMag = (touchPrevPos1 - touchPrevPos2).magnitude;
            float currentMag = (FirstTouch.position - SecondTouch.position).magnitude;

            zoomLevel += -(currentMag - prevMag) * pinchZoomFactor;
            zoomLevel = Mathf.Clamp(zoomLevel, minZoom, maxZoom);
            camera.orthographicSize = zoomLevel;

            var newCamPosition = camera.transform.position;
            var touchesMiddle = FirstTouch.position + (SecondTouch.position - FirstTouch.position) / 2;

            if (!isZooming)
            {
                var mid = camera.ScreenToWorldPoint(touchesMiddle);
                mid.y = 150;
                zoomTarget = mid;
            }
            isZooming = true;

            newCamPosition = Vector3.Lerp(camera.transform.position,
            zoomTarget, Time.deltaTime);
            newCamPosition.y = 200;

            if (FirstTouch.deltaPosition.magnitude > 0.001f && SecondTouch.deltaPosition.magnitude > 0.001 && zoomLevel < maxZoom)
            {
                camera.transform.position = newCamPosition;
            }

        }
        //==== Panning ====
        else if (!isZooming && Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject())
        {

            //Calculate the world point you are clicking
            var currentPoint = GetCursorWorldPoint();

            // Calculate the difference between this frame and last frame.
            var move = lastPoint - currentPoint;

            //Caculate new position.
            var newCameraPosition = camera.transform.position;
            newCameraPosition.x += move.x;
            newCameraPosition.z += move.z;

            camera.transform.position = newCameraPosition;
        }

        if (isZooming && Input.touchCount == 0)
        {
            isZooming = false;
        }

        //==== Zooming ====
        if(Input.mouseScrollDelta != Vector2.zero)
        {
            lastPoint = GetCursorWorldPoint();
            var newCamPosition = camera.transform.position;

            newCamPosition = zoomTarget;
            newCamPosition.y = 200;

            camera.transform.position = newCamPosition;

            zoomTarget = GetCursorWorldPoint();

            zoomLevel += Input.GetAxisRaw("Mouse ScrollWheel") * -zoomSpeed;
            zoomLevel = Mathf.Clamp(zoomLevel, minZoom, maxZoom);
            camera.orthographicSize = zoomLevel;

            //Calculate the world point you are clicking
            var currentPoint = GetCursorWorldPoint();

            // Calculate the difference between this frame and last frame.
            var move = lastPoint - currentPoint;

            //Caculate new position.
            var newCameraPosition = camera.transform.position;
            newCameraPosition.x += move.x;
            newCameraPosition.z += move.z;

            camera.transform.position = newCameraPosition;


        }
        //lastPoint = GetCursorWorldPoint();

    }

    private Vector3 GetCursorWorldPoint()
    {
        var mousePos = Input.mousePosition;
        Vector3 result = camera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 100));
        return result;
    }    
}
