﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using SciterSharp;
#if OSX
using CoreGraphics;
#endif
using IconDrop.Data;

namespace IconDrop.Svg
{
	public class SvgParser
	{
		public BoundsD _bounds;
		public SciterPath _spath = SciterPath.Create();
#if OSX
		public CGPath _cgpath = new CGPath();
#endif

		public SvgScaler _scaler = new SvgScaler();
		private List<float> _operands;
		private string _svg;
		private int _ic = 0;
		private char _last_cmd;
		private float _posx;
		private float _posy;
		private float _last_control_x;
		private float _last_control_y;

		public static SvgParser FromPath(string svgpath)
		{
			var sp = new SvgParser();
			sp.Parse(svgpath);

			#if OSX
			var box = sp._cgpath.BoundingBox;
			sp._bounds.l = box.Left;
			sp._bounds.t = box.Top;
			sp._bounds.w = box.Width;
			sp._bounds.h = box.Height;
			#endif

			return sp;
		}

		public void NormalizeToSize(int size)
		{
			_scaler.OffsetXY(-(float)_bounds.l, -(float)_bounds.t);

			double w = _bounds.w;
			double h = _bounds.h;
			double max = w > h ? w : h;
			double factor = size / max;
			_scaler.Scale((float) factor);

			RefreshBounds();
		}

		public void RefreshBounds()
		{
			_bounds = FromPath(_scaler.ToPath())._bounds;
		}

		private void Parse(string svgpath)
		{
			_svg = svgpath;
			_spath.MoveTo(0, 0);
#if OSX
			_cgpath.MoveToPoint(0, 0);
#endif

			while(_ic != _svg.Length)
			{
				char cmd = _svg[_ic++];
				ReadOperands();

				switch(cmd)
				{
					case 'M': case 'm': AppendMoveTo(cmd=='m'); break;
					case 'L': case 'l': AppendLineTo(cmd == 'l'); break;
					case 'H': case 'h': AppendHLineTo(cmd == 'h'); break;
					case 'V': case 'v': AppendVLineTo(cmd == 'v'); break;

					case 'C': case 'c': AppendCubicCurve(cmd == 'c'); break;
					case 'S': case 's': AppendShorthandCubicCurve(cmd == 's'); break;

					case 'Q': case 'q': AppendQuadraticCurve(cmd == 'q'); break;
					case 'T': case 't': AppendShorthandQuadraticCurve(cmd == 't'); break;

					case 'A': case 'a': AppendArc(cmd == 'a'); break;
						
					case 'Z': case 'z':
						_scaler.AddOperator('Z');
						_spath.ClosePath();
#if OSX
						_cgpath.CloseSubpath();
#endif
						break;
				}

				_last_cmd = cmd;
			}
		}

		private void ReadOperands()
		{
			_operands = new List<float>();
			while(true)
			{
				if(_ic == _svg.Length)
					return;
				
				StringBuilder sb = new StringBuilder();
				char c = _svg[_ic];
				if(char.IsLetter(c))
					return;

				if(c == '-')
				{
					sb.Append(c);
					_ic++;
				}
				else if(c == ' ' || c == '\t' || c == ',')// , is a number separator
				{
					_ic++;
					continue;
				}

				while(true)
				{
					c = _svg[_ic];
					if(char.IsNumber(c) || c=='.')
					{
						sb.Append(c);
						if(++_ic == _svg.Length)
							break;
						continue;
					}
					break;
				}

				string f = sb.ToString();
				_operands.Add(float.Parse(f, CultureInfo.InvariantCulture));
			}
		}

		private void AppendMoveTo(bool relative)
		{
			if(_operands.Count % 2 != 0)
				throw new Exception("Invalid parameter count in M style token");

			for(int i = 0; i < _operands.Count; i+=2)
			{
				float x = _operands[i];
				float y = _operands[i+1];

				if(relative)
				{
					x += _posx;
					y += _posy;
				}
				_spath.MoveTo(x, y);
#if OSX
				_cgpath.MoveToPoint(x, y);
#endif

				_posx = x;
				_posy = y;
				_scaler.AddXYOperands(x, y);
			}
			_scaler.AddOperator('M');
		}

		private void AppendLineTo(bool relative)
		{
			if(_operands.Count % 2 != 0)
				throw new Exception("Invalid parameter count in L style token");

			for(int i = 0; i < _operands.Count; i += 2)
			{
				float x = _operands[i];
				float y = _operands[i + 1];

				if(relative)
				{
					x += _posx;
					y += _posy;
				}
				_spath.LineTo(x, y);
#if OSX
				_cgpath.AddLineToPoint(x, y);
#endif

				_posx = x;
				_posy = y;
				_scaler.AddXYOperands(x, y);
			}
			_scaler.AddOperator('L');
		}

		private void AppendHLineTo(bool relative)
		{
			for(int i = 0; i < _operands.Count; i++)
			{
				float x = _operands[i];
				float y = _posy;

				if(relative)
				{
					x += _posx;
				}
				_spath.LineTo(x, y);
#if OSX
				_cgpath.AddLineToPoint(x, y);
#endif

				_posx = x;
				_scaler.AddXOperand(x);
			}
			_scaler.AddOperator('H');
		}

		private void AppendVLineTo(bool relative)
		{
			for(int i = 0; i < _operands.Count; i++)
			{
				float x = _posx;
				float y = _operands[i];

				if(relative)
				{
					y += _posy;
				}
				_spath.LineTo(x, y);
#if OSX
				_cgpath.AddLineToPoint(x, y);
#endif
				_posy = y;
				_scaler.AddYOperand(y);
			}
			_scaler.AddOperator('V');
		}

		private void AppendCubicCurve(bool relative)
		{
			if(_operands.Count % 6 != 0)
				throw new Exception("Invalid number of parameters for C command");

			for(int i = 0; i < _operands.Count; i+=6)
			{
				float x1 = _operands[i + 0];
				float y1 = _operands[i + 1];
				float x2 = _operands[i + 2];
				float y2 = _operands[i + 3];
				float x = _operands[i + 4];
				float y = _operands[i + 5];

				_spath.BezierCurveTo(x1, y1, x2, y2, x, y, relative);

				if(relative)
				{
					x1 += _posx;
					y1 += _posy;
					x2 += _posx;
					y2 += _posy;
					x += _posx;
					y += _posy;
				}
#if OSX
				_cgpath.AddCurveToPoint(x1, y1, x2, y2, x, y);
#endif

				_last_control_x = x2;
				_last_control_y = y2;

				_posx = x;
				_posy = y;

				_scaler.AddXYOperands(x1, y1);
				_scaler.AddXYOperands(x2, y2);
				_scaler.AddXYOperands(x, y);
			}
			_scaler.AddOperator('C');
		}

		private void AppendShorthandCubicCurve(bool relative)
		{
			if(_operands.Count % 4 != 0)
				throw new Exception("Invalid number of parameters for S command");
			if(_last_cmd != 'C' && _last_cmd != 'c' && _last_cmd != 'S' &&_last_cmd != 's')
			{
				_last_control_x = _posx;
				_last_control_y = _posy;
			}

			for(int i = 0; i < _operands.Count; i += 4)
			{
				float x1 = _posx + (_posx - _last_control_x);
				float y1 = _posy + (_posy - _last_control_y);
				float x2 = _operands[i + 0];
				float y2 = _operands[i + 1];
				float x = _operands[i + 2];
				float y = _operands[i + 3];

				if(relative)
				{
					x2 += _posx;
					y2 += _posy;
					x += _posx;
					y += _posy;
				}

				_spath.BezierCurveTo(x1, y1, x2, y2, x, y);
#if OSX
				_cgpath.AddCurveToPoint(x1, y1, x2, y2, x, y);
#endif

				_last_control_x = x2;
				_last_control_y = y2;

				_posx = x;
				_posy = y;

				_scaler.AddXYOperands(x2, y2);
				_scaler.AddXYOperands(x, y);
			}
			_scaler.AddOperator('S');
		}

		private void AppendQuadraticCurve(bool relative)
		{
			if(_operands.Count % 4 != 0)
				throw new Exception("Invalid number of parameters for Q command");

			for(int i = 0; i < _operands.Count; i += 4)
			{
				float x1 = _operands[i + 0];
				float y1 = _operands[i + 1];
				float x = _operands[i + 2];
				float y = _operands[i + 3];

				if(relative)
				{
					var posx = _posx;
					var posy = _posy;
					x1 += posx;
					y1 += posy;
					x += posx;
					y += posy;
				}
				_spath.QuadraticCurveTo(x1, y1, x, y, relative);
#if OSX
				_cgpath.AddQuadCurveToPoint(x1, y1, x, y);
#endif

				_last_control_x = x1;
				_last_control_y = y1;

				_posx = x;
				_posy = y;

				_scaler.AddXYOperands(x1, y1);
				_scaler.AddXYOperands(x, y);
			}
			_scaler.AddOperator('Q');
		}

		private void AppendShorthandQuadraticCurve(bool relative)
		{
			if(_operands.Count % 2 != 0)
				throw new Exception("Invalid number of parameters for T command");
			if(_last_cmd != 'Q' && _last_cmd != 'q' && _last_cmd != 'T' && _last_cmd != 't')
			{
				_last_control_x = _posx;
				_last_control_y = _posy;
			}

			for(int i = 0; i < _operands.Count; i += 2)
			{
				var posx = _posx;
				var posy = _posy;

				float x1 = posx + (posx - _last_control_x);
				float y1 = posy + (posy - _last_control_y);
				float x = _operands[i + 0];
				float y = _operands[i + 1];

				if(relative)
				{
					x += posx;
					y += posy;
				}

				_spath.QuadraticCurveTo(x1, y1, x, y);
#if OSX
				_cgpath.AddQuadCurveToPoint(x1, y1, x, y);
#endif

				_last_control_x = x1;
				_last_control_y = y1;

				_posx = x;
				_posy = y;

				_scaler.AddXYOperands(x, y);
			}
			_scaler.AddOperator('T');
		}

		private void AppendArc(bool relative)
		{
			if(_operands.Count % 7 != 0)
				throw new Exception("Invalid number of parameters for A command");
			
			Debug.Assert(false);
			for(int i = 0; i < _operands.Count; i += 7)
			{
				float x = _operands[i + 0];
				float y = _operands[i + 1];
				float angle = _operands[i + 2];

				//_cgpath.AddArc()
				//_path.ArcTo();
			}
		}
	}

	public class SvgScaler
	{
		private List<char> _operators = new List<char>();
		private List<List<float>> _operands = new List<List<float>>();
		private List<float> _tmp_operands = new List<float>();

		public void Scale(float factor)
		{
			for(int i = 0; i < _operands.Count; i++)
				for(int j = 0; j < _operands[i].Count; j++)
					_operands[i][j] *= factor;
		}

		public void OffsetXY(float x, float y)
		{
			int iop = 0;
			foreach(var op in _operators)
			{
				switch(op)
				{
					case 'H':
						for(int i = 0; i < _operands[iop].Count; i++)
							_operands[iop][i] += y;
						break;

					case 'V':
						for(int i = 0; i < _operands[iop].Count; i++)
							_operands[iop][i] += x;
						break;
						
					case 'M':
					case 'L':
					case 'C':
					case 'S':
					case 'Q':
					case 'T':
					case 'A':
						for(int i = 0; i < _operands[iop].Count; i+=2)
						{
							_operands[iop][i] += x;
							_operands[iop][i+1] += y;
						}
						break;

					case 'Z':
						break;

					default:
						Debug.Assert(false);
						break;
				}

				iop++;
			}
		}

		public void AddOperator(char c)
		{
			switch(c)
			{
				case 'M': case 'm': Debug.Assert(_tmp_operands.Count % 2 == 0); break;
				case 'L': case 'l': Debug.Assert(_tmp_operands.Count % 2 == 0); break;
				case 'H': case 'h': break;
				case 'V': case 'v': break;

				case 'C': case 'c': Debug.Assert(_tmp_operands.Count % 6 == 0); break;
				case 'S': case 's': Debug.Assert(_tmp_operands.Count % 4 == 0); break;

				case 'Q': case 'q': Debug.Assert(_tmp_operands.Count % 4 == 0); break;
				case 'T': case 't': Debug.Assert(_tmp_operands.Count % 2 == 0); break;
			}
			_operators.Add(c);
			_operands.Add(_tmp_operands.ToList());
			_tmp_operands.Clear();
		}

		public void AddXYOperands(float x, float y)
		{
			AddXOperand(x);
			AddYOperand(y);
		}
		public void AddXOperand(float x)
		{
			_tmp_operands.Add(x);
		}
		public void AddYOperand(float y)
		{
			_tmp_operands.Add(y);
		}

		public string ToPath()
		{
			Debug.Assert(_operators.Count == _operands.Count);

			StringBuilder sb = new StringBuilder();
			for(int i = 0; i < _operators.Count; i++)
			{
				sb.Append(_operators[i]);
				foreach(var f in _operands[i])
				{
					string ftos = f.ToString("0.########", CultureInfo.InvariantCulture);
					Debug.Assert(!ftos.Contains(','));
					Debug.Assert(!ftos.Contains('E'));
					sb.Append(' ');
					sb.Append(ftos);
				}
			}
			return sb.ToString();
		}
	}
}