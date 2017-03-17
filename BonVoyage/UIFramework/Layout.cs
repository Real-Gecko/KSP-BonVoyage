/*
 * UI Framework licensed under BSD 3-clause license
 * https://github.com/Real-Gecko/Unity5-UIFramework/blob/master/LICENSE.md
*/

using System;
using UnityEngine;

namespace BonVoyage
{
	public class Layout
	{
		/// <summary>
		/// Styled label with white text color.
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="options">Options.</param>
		public static void Label(string text, params GUILayoutOption[] options) {
			Styles.label.normal.textColor = Color.white;
			Styles.label.alignment = TextAnchor.MiddleLeft;
			Styles.label.stretchWidth = false;
			GUILayout.Label (text, Styles.label, options);
		}

		/// <summary>
		/// Styled label with text color accepted as argument.
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="color">Color.</param>
		/// <param name="options">Options.</param>
		public static void Label(string text, Color color, params GUILayoutOption[] options) {
			Styles.label.normal.textColor = color;
			Styles.label.alignment = TextAnchor.MiddleLeft;
			Styles.label.stretchWidth = false;
			GUILayout.Label (text, Styles.label, options);
		}

		/// <summary>
		/// Styled label with center text alignment
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="options">Options.</param>
		public static void LabelCentered(string text, params GUILayoutOption[] options) {
			Styles.label.normal.textColor = Color.white;
			Styles.label.alignment = TextAnchor.MiddleCenter;
			Styles.label.stretchWidth = true;
			GUILayout.Label (text, Styles.label, options);
		}

		/// <summary>
		/// Styled label with center text alignmet and color
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="color">Color.</param>
		/// <param name="options">Options.</param>
		public static void LabelCentered(string text, Color color, params GUILayoutOption[] options) {
			Styles.label.normal.textColor = color;
			Styles.label.alignment = TextAnchor.MiddleCenter;
			Styles.label.stretchWidth = true;
			GUILayout.Label (text, Styles.label, options);
		}

		/// <summary>
		/// Styled label with text aligned to the right
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="options">Options.</param>
		public static void LabelRight(string text, params GUILayoutOption[] options) {
			Styles.label.normal.textColor = Color.white;
			Styles.label.alignment = TextAnchor.MiddleRight;
			Styles.label.stretchWidth = false;
			GUILayout.Label (text, Styles.label, options);
		}

		/// <summary>
		/// Styled label with colored text aligned to the right
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="color">Color.</param>
		/// <param name="options">Options.</param>
		public static void LabelRight(string text, Color color, params GUILayoutOption[] options) {
			Styles.label.normal.textColor = color;
			Styles.label.alignment = TextAnchor.MiddleRight;
			Styles.label.stretchWidth = false;
			GUILayout.Label (text, Styles.label, options);
		}

		/// <summary>
		/// Styled button with white text color
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="options">Options.</param>
		public static bool Button(string text, params GUILayoutOption[] options) {
			Styles.button.normal.textColor = Color.white;
			Styles.button.alignment = TextAnchor.MiddleCenter;
			Styles.button.stretchWidth = true;
			return GUILayout.Button (text, Styles.button, options);
		}

		/// <summary>
		/// Styled button with text color accepted as argument.
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="color">Color.</param>
		/// <param name="options">Options.</param>
		public static bool Button(string text, Color color, params GUILayoutOption[] options) {
			Styles.button.normal.textColor = color;
			Styles.button.alignment = TextAnchor.MiddleCenter;
			Styles.button.stretchWidth = true;
			return GUILayout.Button (text, Styles.button, options);
		}

		/// <summary>
		/// Styled button with text aligned to the left
		/// </summary>
		/// <returns><c>true</c>, if left was buttoned, <c>false</c> otherwise.</returns>
		/// <param name="text">Text.</param>
		/// <param name="options">Options.</param>
		public static bool ButtonLeft(string text, params GUILayoutOption[] options) {
			Styles.button.normal.textColor = Color.white;
			Styles.button.alignment = TextAnchor.MiddleLeft;
			Styles.button.stretchWidth = true;
			return GUILayout.Button (text, Styles.button, options);
		}

		/// <summary>
		/// Styled button with text aligned to the left and color as argument
		/// </summary>
		/// <returns><c>true</c>, if left was buttoned, <c>false</c> otherwise.</returns>
		/// <param name="text">Text.</param>
		/// <param name="color">Color.</param>
		/// <param name="options">Options.</param>
		public static bool ButtonLeft(string text, Color color, params GUILayoutOption[] options) {
			Styles.button.normal.textColor = color;
			Styles.button.alignment = TextAnchor.MiddleLeft;
			Styles.button.stretchWidth = true;
			return GUILayout.Button (text, Styles.button, options);
		}

		/// <summary>
		/// Creates label with "label: text" with different colors in one line
		/// </summary>
		/// <param name="label">Label.</param>
		/// <param name="text">Text.</param>
		public static void LabelAndText(string label, string text) {
			GUILayout.BeginHorizontal ();
			Label (label + ": ", Palette.blue);
			Label (text, Color.white);
			GUILayout.EndHorizontal ();
		}

		/// <summary>
		/// Margin with the specified width.
		/// </summary>
		/// <param name="width">Width.</param>
		public static void Margin(int width) {
			GUILayout.Label ("", Styles.label, GUILayout.Width(width));
		}

		/// <summary>
		/// Styled scrollview
		/// </summary>
		/// <returns>The scroll view.</returns>
		/// <param name="scrollPos">Scroll position.</param>
		/// <param name="options">Options.</param>
		public static Vector2 BeginScrollView(Vector2 scrollPos, params GUILayoutOption[] options) {
			return GUILayout.BeginScrollView (scrollPos, false, true, Styles.verticalScrollbarThumb, Styles.verticalScrollbarThumb, Styles.scrollView, options);
		}

		/// <summary>
		/// Horizontal separator of the specified height
		/// </summary>
		public static void HR(int height = 20) {
			GUILayout.Label ("", Styles.label, GUILayout.Height(height));
		}

		/// <summary>
		/// Selection Grid.
		/// </summary>
		/// <returns>The grid.</returns>
		/// <param name="selected">Selected.</param>
		/// <param name="captions">Captions.</param>
		/// <param name="count">Count.</param>
		/// <param name="options">Options.</param>
		public static int SelectionGrid(int selected, string[] captions, int count, params GUILayoutOption[] options) {
			return GUILayout.SelectionGrid (selected, captions, count, Styles.selectionGrid, options);
		}

		public static bool Toggle(bool value, string text, params GUILayoutOption[] options) {
			string prefix = value ? "● " : "○ ";
			return GUILayout.Toggle (value, prefix + text, Styles.toggle, options);
		}

		/// <summary>
		/// Styled window
		/// </summary>
		/// <param name="id">Identifier.</param>
		/// <param name="screenRect">Screen rect.</param>
		/// <param name="func">Func.</param>
		/// <param name="title">Title.</param>
		/// <param name="options">Options.</param>
		public static Rect Window(int id, Rect screenRect, GUI.WindowFunction func, string title, params GUILayoutOption[] options) {
			// Fix rect width and height not being integers to avoid blurry font rendering
			screenRect.width = (float)Math.Floor (screenRect.width);
			screenRect.height = (float)Math.Floor (screenRect.height);
			return GUILayout.Window (id, screenRect, func, title, Styles.window, options);
		}

		/// <summary>
		/// Horizontalslider.
		/// </summary>
		/// <returns>The slider.</returns>
		/// <param name="value">Value.</param>
		/// <param name="leftValue">Left value.</param>
		/// <param name="rightValue">Right value.</param>
		/// <param name="options">Options.</param>
		public static float HorizontalSlider(float value, float leftValue, float rightValue, params GUILayoutOption[] options) {
			return GUILayout.HorizontalSlider (
				value,
				leftValue,
				rightValue,
				Styles.horizontalSlider,
				Styles.horizontalSliderThumb,
				options
			);
		}

		/// <summary>
		/// Double version of HorizontalSlider
		/// </summary>
		/// <returns>The slider.</returns>
		/// <param name="value">Value.</param>
		/// <param name="leftValue">Left value.</param>
		/// <param name="rightValue">Right value.</param>
		/// <param name="options">Options.</param>
		public static double HorizontalSlider(double value, double leftValue, double rightValue, params GUILayoutOption[] options) {
			return (double)GUILayout.HorizontalSlider (
				(float)value,
				(float)leftValue,
				(float)rightValue,
				Styles.horizontalSlider,
				Styles.horizontalSliderThumb,
				options
			);
		}
	}
}

