// Package bc7 decodes BC7 (a.k.a. BPTC_RGBA) block-compressed images to RGBA.
//
// BC7 packs a 4×4 block of RGBA8 pixels into 128 bits using one of 8 modes.
// This implementation follows the Khronos ARB_texture_compression_bptc
// specification.
package bc7

import (
	"fmt"
	"image"
	"image/color"
	"math/bits"
)

// DecodeRGBA decodes w×h BC7-compressed pixels (w,h multiples of 4) into an
// RGBA image. The block layout is row-major: blocks of 4×4 pixels enumerated
// left-to-right, top-to-bottom, 16 bytes per block.
func DecodeRGBA(data []byte, w, h int) (*image.RGBA, error) {
	if w <= 0 || h <= 0 || w%4 != 0 || h%4 != 0 {
		return nil, fmt.Errorf("bc7: dimensions must be positive multiples of 4, got %dx%d", w, h)
	}
	blocksW, blocksH := w/4, h/4
	want := blocksW * blocksH * 16
	if len(data) < want {
		return nil, fmt.Errorf("bc7: need %d bytes for %dx%d, got %d", want, w, h, len(data))
	}
	out := image.NewRGBA(image.Rect(0, 0, w, h))
	for by := 0; by < blocksH; by++ {
		for bx := 0; bx < blocksW; bx++ {
			var block [16]color.RGBA
			if err := decodeBlock(data[(by*blocksW+bx)*16:][:16], &block); err != nil {
				return nil, fmt.Errorf("bc7: block (%d,%d): %w", bx, by, err)
			}
			for py := 0; py < 4; py++ {
				for px := 0; px < 4; px++ {
					out.SetRGBA(bx*4+px, by*4+py, block[py*4+px])
				}
			}
		}
	}
	return out, nil
}

// bitReader reads little-endian-bit-order bits from a byte slice.
type bitReader struct {
	buf    []byte
	bitPos int
}

// get reads up to 32 bits and returns them as a uint32. Bits are read LSB-
// first across bytes: byte 0 bit 0 is the first bit, byte 0 bit 7 is the
// 8th, byte 1 bit 0 is the 9th, etc.
func (r *bitReader) get(n int) uint32 {
	if n == 0 {
		return 0
	}
	var v uint32
	for i := 0; i < n; i++ {
		b := (r.buf[r.bitPos>>3] >> (r.bitPos & 7)) & 1
		v |= uint32(b) << i
		r.bitPos++
	}
	return v
}

func (r *bitReader) skip(n int) { r.bitPos += n }

func decodeBlock(block []byte, pixels *[16]color.RGBA) error {
	// Find mode by counting trailing zero bits of byte 0 (up to 7).
	mode := 0
	for ; mode < 8; mode++ {
		if block[0]&(1<<mode) != 0 {
			break
		}
	}
	if mode == 8 {
		// All zeros in low 8 bits — spec says this is invalid; emit black.
		return nil
	}
	m := modes[mode]

	r := bitReader{buf: block}
	r.skip(mode + 1) // mode bits

	partition := uint8(r.get(m.partitionBits))
	rotation := uint8(r.get(m.rotationBits))
	var idxMode uint8
	if m.idxModeBits == 1 {
		idxMode = uint8(r.get(1))
	}

	// Read raw endpoint bits for RGB(A) channels: planes = 3 (RGB) or 4 (RGBA).
	nEP := m.colorEndpoints
	var rawR, rawG, rawB, rawA [6]uint32
	for i := 0; i < nEP; i++ {
		rawR[i] = r.get(m.colorBits)
	}
	for i := 0; i < nEP; i++ {
		rawG[i] = r.get(m.colorBits)
	}
	for i := 0; i < nEP; i++ {
		rawB[i] = r.get(m.colorBits)
	}
	if m.alphaBits > 0 {
		for i := 0; i < nEP; i++ {
			rawA[i] = r.get(m.alphaBits)
		}
	}

	// P-bits, if any.
	var pbits [6]uint32
	switch m.pBitKind {
	case 1: // per-endpoint
		for i := 0; i < nEP; i++ {
			pbits[i] = r.get(1)
		}
	case 2: // shared per subset (only mode 1, 2 subsets)
		for i := 0; i < m.subsets; i++ {
			s := r.get(1)
			pbits[i*2] = s
			pbits[i*2+1] = s
		}
	}

	// Reconstruct 8-bit endpoints.
	var ep [6]color.RGBA
	for i := 0; i < nEP; i++ {
		colorTotal := m.colorBits
		alphaTotal := m.alphaBits
		rv, gv, bv, av := rawR[i], rawG[i], rawB[i], rawA[i]
		if m.pBitKind != 0 {
			rv = (rv << 1) | pbits[i]
			gv = (gv << 1) | pbits[i]
			bv = (bv << 1) | pbits[i]
			if m.alphaBits > 0 {
				av = (av << 1) | pbits[i]
				alphaTotal++
			}
			colorTotal++
		}
		ep[i].R = replicate8(rv, colorTotal)
		ep[i].G = replicate8(gv, colorTotal)
		ep[i].B = replicate8(bv, colorTotal)
		if m.alphaBits > 0 {
			ep[i].A = replicate8(av, alphaTotal)
		} else {
			ep[i].A = 0xFF
		}
	}

	// Primary indices (per pixel).
	// Each subset's anchor pixel has 1 fewer bit (MSB implicit 0).
	// Figure out which pixels are anchors in the current partition.
	var anchorPix [3]int
	anchorPix[0] = 0 // subset 0
	switch m.subsets {
	case 2:
		anchorPix[1] = int(anchor2[partition])
	case 3:
		anchorPix[1] = int(anchor3_2[partition])
		anchorPix[2] = int(anchor3_3[partition])
	}

	// Determine subset of each pixel.
	var subsetOf [16]uint8
	switch m.subsets {
	case 1:
		// all zero
	case 2:
		for i := 0; i < 16; i++ {
			subsetOf[i] = partition2[partition][i]
		}
	case 3:
		for i := 0; i < 16; i++ {
			subsetOf[i] = partition3[partition][i]
		}
	}

	var idx1 [16]uint8
	for i := 0; i < 16; i++ {
		bitsFor := m.indexBits
		if isAnchor(i, anchorPix[:m.subsets]) {
			bitsFor--
		}
		idx1[i] = uint8(r.get(bitsFor))
	}

	var idx2 [16]uint8
	if m.index2Bits > 0 {
		// Secondary indices only exist in modes 4, 5 (single subset).
		// Anchor is pixel 0.
		for i := 0; i < 16; i++ {
			bitsFor := m.index2Bits
			if i == 0 {
				bitsFor--
			}
			idx2[i] = uint8(r.get(bitsFor))
		}
	}

	// Interpolate and write pixels.
	primaryWeights := weightsFor(m.indexBits)
	secondaryWeights := weightsFor(m.index2Bits)

	for i := 0; i < 16; i++ {
		s := subsetOf[i]
		e0 := ep[s*2]
		e1 := ep[s*2+1]
		var c color.RGBA
		// Select color vs alpha index source.
		// Default: primary = color, secondary = alpha.
		// If idxMode == 1 (mode 4 only), swap their roles.
		var colorIdx, alphaIdx int
		var colorW, alphaW []int
		if m.index2Bits == 0 {
			colorIdx = int(idx1[i])
			alphaIdx = colorIdx
			colorW = primaryWeights
			alphaW = primaryWeights
		} else {
			if idxMode == 0 {
				colorIdx = int(idx1[i])
				alphaIdx = int(idx2[i])
				colorW = primaryWeights
				alphaW = secondaryWeights
			} else {
				colorIdx = int(idx2[i])
				alphaIdx = int(idx1[i])
				colorW = secondaryWeights
				alphaW = primaryWeights
			}
		}
		c.R = interp(e0.R, e1.R, colorW[colorIdx])
		c.G = interp(e0.G, e1.G, colorW[colorIdx])
		c.B = interp(e0.B, e1.B, colorW[colorIdx])
		c.A = interp(e0.A, e1.A, alphaW[alphaIdx])

		// Rotation (modes 4 and 5): swap channels.
		switch rotation {
		case 1:
			c.R, c.A = c.A, c.R
		case 2:
			c.G, c.A = c.A, c.G
		case 3:
			c.B, c.A = c.A, c.B
		}

		pixels[i] = c
	}
	return nil
}

func isAnchor(i int, anchors []int) bool {
	for _, a := range anchors {
		if a == i {
			return true
		}
	}
	return false
}

// replicate8 widens an n-bit value to 8 bits using bit replication.
func replicate8(v uint32, n int) uint8 {
	if n == 0 {
		return 0
	}
	if n >= 8 {
		return uint8(v)
	}
	v <<= uint(8 - n)
	// Fill low bits by copying high bits down.
	v |= v >> uint(n)
	return uint8(v & 0xFF)
}

// interp returns ((64-w)*a + w*b + 32) >> 6.
func interp(a, b uint8, w int) uint8 {
	return uint8((int(a)*(64-w) + int(b)*w + 32) >> 6)
}

// unused but kept for self-documentation: mode detection helper.
var _ = bits.TrailingZeros8
