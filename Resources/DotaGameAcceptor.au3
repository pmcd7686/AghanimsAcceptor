Sleep(5000) ; Give some fucking time before you start this shit!

Global $mouseCoordinates[2] = Mousegetpos()
Global $updatedMouseCoordinates[2] = Mousegetpos()

Global $startingXPosition = $mouseCoordinates[0]
Global $startingYPosition = $mouseCoordinates[1]
Global $updateXPosition = $updatedMouseCoordinates[0]
Global $updateYPosition = $updatedMouseCoordinates[1]

While True
   Sleep(100)
   $updatedMouseCoordinates = Mousegetpos()         ; UPDATE THOSE MOUSE COOOOOORDSS
   $updateXPosition = $updatedMouseCoordinates[0]
   $updateYPosition = $updatedMouseCoordinates[1]

   if (Abs($startingXPosition - $updateXPosition) > 5 Or Abs($startingYPosition - $updateYPosition) > 5) Then
	  ;MsgBox(0,"","MOUSE MOVED")
	  Exit 2 ; QUIT THIS SHIT! SOMEBODY IS HERE!
   EndIf

   if WinActive("Dota 2") Then
	  Sleep(1000)
	  Send("{ENTER}")
	  Exit 1 ; Success! WOO HOO
   EndIf

WEnd