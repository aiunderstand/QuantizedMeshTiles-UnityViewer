using System;
using System.Collections;
using System.Collections.Generic;
using Terrain;
using TMPro;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    Transform _target;
    public float Speed = 2;
    public float PanSpeed = 0.5f;
    Vector3 lastPosition;

    public GameObject[] anchors;
    public int _currentAnchorId = 0;
    bool isAnimating = false;

    /// <summary>
    /// Normal speed of camera movement.
    /// </summary>
    public float movementSpeed = 10f;

    /// <summary>
    /// Speed of camera movement when shift is held down,
    /// </summary>
    public float fastMovementSpeed = 100f;

    /// <summary>
    /// Sensitivity for free look.
    /// </summary>
    public float freeLookSensitivity = 3f;

    /// <summary>
    /// Amount to zoom the camera when using the mouse wheel.
    /// </summary>
    public float zoomSensitivity = 500f;

    /// <summary>
    /// Amount to zoom the camera when using the mouse wheel (fast mode).
    /// </summary>
    public float fastZoomSensitivity = 50f;

    /// <summary>
    /// Set to true when free looking (on right mouse button).
    /// </summary>
    private bool looking = false;

    
    private bool allowRightMouseBtn = true;

    enum ViewMode
    {
        _3D,
        _2D
    }

    private ViewMode viewMode;
    private TMP_Dropdown viewmodeDropdown;

    enum ScrollwheelMode
    {
        Zoom,
        StoryMap //use for next or previous slide (gameobject anchors) 
    }

    private ScrollwheelMode scrollwheelMode;
    private TMP_Dropdown scrollwheelDropdown;


    void Awake()
    {
        _target = anchors[0].transform;

        InitUI();
    }

    private void InitUI()
    {
        var viewmodeComponent = MapViewer.GetComponentUI("View mode");

        if (viewmodeComponent != null)
        {
            viewmodeDropdown = viewmodeComponent.GetComponent<TMP_Dropdown>();
            UpdateViewMode(viewmodeDropdown.value);
        }
        else
        {
            Debug.LogError("view mode UI component not found");
        }


        var scrollwheelComponent = MapViewer.GetComponentUI("Scroll wheel");

        if (scrollwheelComponent != null)
        {
            scrollwheelDropdown = scrollwheelComponent.GetComponent<TMP_Dropdown>();
            scrollwheelMode = (ScrollwheelMode)Enum.Parse(typeof(ScrollwheelMode), scrollwheelDropdown.options[scrollwheelDropdown.value].text);
        }
        else
        {
            Debug.LogError("scroll wheel UI component not found");
        }
    }

    public void UpdateViewMode(int index)
    {
        viewMode = (ViewMode)Enum.Parse(typeof(ViewMode), "_" + viewmodeDropdown.options[index].text);

        switch (viewMode)
        {
            case ViewMode._2D:
                //animate camera to top view and disable right mouse (free look)
                allowRightMouseBtn = false;

                //if camera height is below 1000 meter set to 1000 for 2D to have value;
                if (_target.position.y < 1000)
                    _target.position = new Vector3(_target.position.x, 1000, _target.position.z);

                _target.rotation = Quaternion.Euler(new Vector3(90, _target.rotation.eulerAngles.y, _target.rotation.eulerAngles.z));
                break;
            case ViewMode._3D:
                //animate camera to top view and disable right mouse (free look)
                allowRightMouseBtn = true;

                //if camera height is above 1000 meter set to 1000 for 3D to have value;
                if (_target.position.y > 1000)
                    _target.position = new Vector3(_target.position.x, 1000, _target.position.z);

                _target.rotation = Quaternion.Euler(new Vector3(45, _target.rotation.eulerAngles.y, _target.rotation.eulerAngles.z));
                break;
        }

        AnimateTo(_target);
    }

    public void UpdateZoomHandler(int index)
    {
        scrollwheelMode = (ScrollwheelMode)Enum.Parse(typeof(ScrollwheelMode), scrollwheelDropdown.options[index].text);
    }

    void AnimateTo(Transform target)
    {
        isAnimating = true;
        _target = target;
    }

    void Update()
    {
        Zoom(); //rotates between vantage points

        PanRotate();

        if (isAnimating)
        {
            transform.position = Vector3.Lerp(transform.position, _target.position, Time.deltaTime * Speed);
            transform.rotation = Quaternion.Lerp(transform.rotation, _target.rotation, Time.deltaTime * Speed);

            if (Vector3.Distance(transform.position, _target.position) < 1f)
            {
                isAnimating = false;
                transform.position = _target.position;
                transform.rotation = _target.rotation;
            }
        }
    }

    void OnDisable()
    {
        StopLooking();
    }

    /// <summary>
    /// Enable free looking.
    /// </summary>
    void StartLooking()
    {
        looking = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>
    /// Disable free looking.
    /// </summary>
    void StopLooking()
    {
        looking = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    
    private void PanRotate()
    {
        if (!isAnimating)
        {
            var fastMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var movementSpeed = fastMode ? this.fastMovementSpeed : this.movementSpeed;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                transform.position = transform.position + (-transform.right * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                transform.position = transform.position + (transform.right * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                transform.position = transform.position + (transform.forward * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                transform.position = transform.position + (-transform.forward * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.Q))
            {
                transform.position = transform.position + (transform.up * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.E))
            {
                transform.position = transform.position + (-transform.up * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.PageUp))
            {
                transform.position = transform.position + (Vector3.up * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.PageDown))
            {
                transform.position = transform.position + (-Vector3.up * movementSpeed * Time.deltaTime);
            }

            if (looking)
            {
                float newRotationX = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * freeLookSensitivity;
                float newRotationY = transform.localEulerAngles.x - Input.GetAxis("Mouse Y") * freeLookSensitivity;
                transform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
            }

            if (Input.GetKeyDown(KeyCode.Mouse1) && allowRightMouseBtn)
            {
                StartLooking();
            }
            else if (Input.GetKeyUp(KeyCode.Mouse1) && allowRightMouseBtn)
            {
                StopLooking();
            }

            if (Input.GetMouseButtonDown(0))
            {
                lastPosition = Input.mousePosition;
            }

            if (Input.GetMouseButton(0))
            {
                var delta = Input.mousePosition - lastPosition;
                transform.Translate(delta.x * PanSpeed, delta.y * PanSpeed, 0);
                lastPosition = Input.mousePosition;
            }
        }
    }

    private void Zoom()
    {
        var d = Input.GetAxis("Mouse ScrollWheel");

        if (scrollwheelMode == ScrollwheelMode.Zoom)
        {
            transform.position = transform.position + transform.forward * d * zoomSensitivity;
        }

        if (scrollwheelMode == ScrollwheelMode.StoryMap)
        {
            if (d > 0f) // scroll up
            {
                if (_currentAnchorId + 1 < anchors.Length)
                {
                    _currentAnchorId++;
                    AnimateTo(anchors[_currentAnchorId].transform);
                }
            }
            else if (d < 0f)   // scroll down
            {

                if (_currentAnchorId - 1 >= 0)
                {
                    _currentAnchorId--;
                    AnimateTo(anchors[_currentAnchorId].transform);
                }
            }
        }
    }
}

