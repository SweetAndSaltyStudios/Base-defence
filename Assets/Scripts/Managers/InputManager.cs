﻿using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : Singelton<InputManager>
{
    #region VARIABLES

    private bool isZooming = false;
	private readonly float cameraPanSpeed = 200f;
	private readonly float mouseScrollSpeed = 2000F;
	private CameraEngine mainCameraEngine;
	private LayerMask whatIsMoveableObject;
	private LayerMask whatIsGround;
	private Ray mouseRay;
	private RaycastHit mouseHit;
	private RaycastHit mouseHitPosition;
	private Vector3 mousePositionRelativeToGround;

#if ANDROID

    private Vector2 firstTouchPreviousPosition;
	private Vector2 secondTouchPreviousPosition;

	private Touch firstTouch;
	private Touch secondTouch;

	private float touchesPreviousPositionDifference;
	private float touchesCurrentPositionDifference;
	private float zoomModifier;
	private readonly float zoomModifierSpeed = 0.1f;

#endif

#endregion VARIABLES

#region PROPERTIES

	public SelectableObject CurrentlySelectableObject { get; set; }
	public SelectableRadius SelectableRadius
	{
		get;
		private set;
	}

	public Vector3 MouseHitPoint
	{
		get
		{
			return mouseHitPosition.point;
		}
	}
	public bool DoWeHaveTouches
	{
		get
		{
			return Input.touchCount > 0;
		}
	}
	public bool MoveableObjectGrabbed
	{
		get;
		private set;
	}
    public bool IsValidPlacement
    {
        get; private set;
    }

#endregion PROPERTIES

    public bool IsOverUIElement()
	{
#if UNITY_EDITOR
		return EventSystem.current.IsPointerOverGameObject();
#else
		return EventSystem.current.IsPointerOverGameObject(firstTouch.fingerId);
#endif
	}

	private Vector3 UpdatePosition()
	{
		return new Vector3  (
							mousePositionRelativeToGround.x,
							mousePositionRelativeToGround.y,
							mousePositionRelativeToGround.z
							);
	}

	private void Start ()
	{
		Initialize();
	}

	private void Initialize()
	{
		whatIsMoveableObject = LayerMask.GetMask("MoveableObject");
		whatIsGround = LayerMask.GetMask("Ground");
		mainCameraEngine = Camera.main.GetComponentInParent<CameraEngine>();
		//mouseHit = new RaycastHit();

		SelectableRadius = ObjectPoolManager.Instance.GetObjectFromPool
			(
			"SelectableRadius",
			Vector3.zero, 
			Quaternion.Euler(new Vector3(90, 0, 0)), 
			false
			).GetComponent<SelectableRadius>();
	}

	private void Update()
	{
#if UNITY_EDITOR

		mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);

		if (Input.GetMouseButtonDown(0))
		{
			FindAndGrabMoveableObject();
		}   

		if (Input.GetMouseButton(0))
		{
			CheckMoveableObject();         
		}

		if (Input.GetMouseButtonUp(0))
		{
			DropMoveableObject();
		}

		MouseScroll();

#elif UNITY_ANDROID

        mouseRay = Camera.main.ScreenPointToRay(Input.GetTouch(0).position);

		if (DoWeHaveTouches)
		{
			OnFirstTouch();

			if (Input.touchCount == 2)
			{
				OnSecondTouch();
			}
		}		
#endif
	}

	private void CheckMoveableObject()
	{
		MoveableObjectGrabbed = CurrentlySelectableObject != null;

		TraceMousePositionRelativeToGround();

		SetSelectableRadiusColor();
	}

	private void FindAndGrabMoveableObject()
	{
		if (Physics.Raycast(mouseRay, out mouseHit, Mathf.Infinity, whatIsMoveableObject))
		{
			var newSelectableObject = mouseHit.transform.GetComponent<SelectableObject>();

			if (newSelectableObject != null)
			{
				CurrentlySelectableObject = newSelectableObject;
				CurrentlySelectableObject.BeingSelected();
				SelectableRadius.ShowViewRadius(CurrentlySelectableObject.transform, CurrentlySelectableObject.SelectableRadius);
			}
		}
		else
		{
			CurrentlySelectableObject = null;
			SelectableRadius.DisableViewRadius();
		}
	}

	private void TraceMousePositionRelativeToGround()
	{
		if (Physics.Raycast(mouseRay, out mouseHitPosition, Mathf.Infinity, whatIsGround))
		{
			if (MoveableObjectGrabbed)
			{
				mousePositionRelativeToGround = mouseHitPosition.point;

				CurrentlySelectableObject.transform.position = UpdatePosition();
			}			
			else
			{
				if (!isZooming && !IsOverUIElement())
				{
					PanCamera();
				}					
			}
		}
	}

	private void DropMoveableObject()
	{
		if (CurrentlySelectableObject == null)
		{
			return;
		}
		
		if (!IsOverUIElement() && !CurrentlySelectableObject.IsCollision)
		{
			 Debug.Log("SuccessfulPlacement");
			CurrentlySelectableObject.SuccessfulPlacement();
		}
		else if (CurrentlySelectableObject.IsFirstPlacement)
		{
			Debug.LogError("ReturnObjectToPool");
			ObjectPoolManager.Instance.ReturnObjectToPool(CurrentlySelectableObject.gameObject);
		}
		else
		{ 
			Debug.LogWarning("ReplaceObject");
			CurrentlySelectableObject.ReplaceObject();
		}

        // Reset values after we do not have control over an object
        IsValidPlacement = SelectableRadius.ShowDoWeHaveValidPositionInColor(true);
        
        // SelectableRadius.Show();
		MoveableObjectGrabbed = false;
	}

	public void SetSelectableRadiusColor()
	{
		if(CurrentlySelectableObject != null)
		{
            IsValidPlacement = SelectableRadius.ShowDoWeHaveValidPositionInColor(IsOverUIElement() || CurrentlySelectableObject.IsCollision ? false : true);
		}		
	}

#if UNITY_ANDROID

	private void OnFirstTouch()
	{
		firstTouch = Input.GetTouch(0);

		switch (firstTouch.phase)
		{
			case TouchPhase.Began:

				FindAndGrabMoveableObject();

				break;

			case TouchPhase.Moved:

				CheckMoveableObject();

				break;

			case TouchPhase.Ended:

				isZooming = false;

				DropMoveableObject();

				break;		
		}
	}

	private void OnSecondTouch()
	{       
		secondTouch = Input.GetTouch(1);

		switch (secondTouch.phase)
		{
			case TouchPhase.Began:

				isZooming = true;

				break;

			case TouchPhase.Moved:

				PinchZoom();

				break;

			case TouchPhase.Ended:

				isZooming = false;

				break;
		}
	}

	public void PinchZoom()
	{
		firstTouchPreviousPosition = firstTouch.position - firstTouch.deltaPosition;
		secondTouchPreviousPosition = secondTouch.position - secondTouch.deltaPosition;

		touchesPreviousPositionDifference = (firstTouchPreviousPosition - secondTouchPreviousPosition).magnitude;
		touchesCurrentPositionDifference = (firstTouch.position - secondTouch.position).magnitude;

		zoomModifier = (firstTouch.deltaPosition - secondTouch.deltaPosition).magnitude * zoomModifierSpeed;

		//if (touchesPreviousPositionDifference > touchesCurrentPositionDifference)
		//{
		//	mainCameraEngine.ZoomOrthographicSize(zoomModifier);
		//}
		//else
		//{
		//	mainCameraEngine.ZoomOrthographicSize(-zoomModifier);
		//}

        mainCameraEngine.ZoomOrthographicSize(touchesPreviousPositionDifference > touchesCurrentPositionDifference ? zoomModifier : -zoomModifier);

    }

#endif

	private void PanCamera()
	{
		float cameraX = 0f;
		float cameraY = 0f;

#if UNITY_EDITOR

		cameraX = Input.GetAxis("Mouse X") * cameraPanSpeed * Time.deltaTime;
		cameraY = Input.GetAxis("Mouse Y") * cameraPanSpeed * Time.deltaTime;


#elif UNITY_ANDROID

		Vector2 touchDeltaPosition = Input.GetTouch(0).deltaPosition;
		cameraX -= touchDeltaPosition.x * cameraPanSpeed / 2 * Time.deltaTime;
		cameraY -= touchDeltaPosition.y * cameraPanSpeed / 2 * Time.deltaTime;	
		
#endif

		mainCameraEngine.MoveCamera(new Vector2(cameraX,cameraY));
	}

	private void MouseScroll()
	{
		var scroll = Input.GetAxis("Mouse ScrollWheel");
		mainCameraEngine.ZoomOrthographicSize(-scroll * mouseScrollSpeed * Time.deltaTime);
	}
}
