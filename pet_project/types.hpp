#ifndef PET_PROJECT_TYPES_H_
#define PET_PROJECT_TYPES_H_

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <random>
#include <vector>

namespace evo {

struct Color {
  uint8_t r;
  uint8_t g;
  uint8_t b;
};

struct Gene {
  Color color;
};

using DNA = std::vector<Gene>;

struct Individual {
  DNA dna;
  float fitness;
};

inline Gene random_gene(std::mt19937& rng) {
  std::uniform_int_distribution<int> col(0, 255);
  return Gene{{static_cast<uint8_t>(col(rng)), static_cast<uint8_t>(col(rng)),
               static_cast<uint8_t>(col(rng))}};
}

inline void mutate_gene(Gene& g, std::mt19937& rng) {
  std::uniform_int_distribution<int> delta(-20, 20);

  int r = static_cast<int>(g.color.r) + delta(rng);
  int gr = static_cast<int>(g.color.g) + delta(rng);
  int b = static_cast<int>(g.color.b) + delta(rng);

  g.color.r = static_cast<uint8_t>(std::clamp(r, 0, 255));
  g.color.g = static_cast<uint8_t>(std::clamp(gr, 0, 255));
  g.color.b = static_cast<uint8_t>(std::clamp(b, 0, 255));
}

inline DNA random_dna(int length, std::mt19937& rng) {
  DNA dna(length);
  for (auto& gene : dna) gene = random_gene(rng);
  return dna;
}

// Subdivide DNA: double the grid by copying each cell into a 2x2 block
inline DNA subdivide_dna(const DNA& dna) {
  int old_grid = static_cast<int>(std::sqrt(dna.size()));
  int new_grid = old_grid * 2;
  DNA result(new_grid * new_grid);

  for (int oy = 0; oy < old_grid; oy++) {
    for (int ox = 0; ox < old_grid; ox++) {
      const Gene& gene = dna[oy * old_grid + ox];
      for (int dy = 0; dy < 2; dy++) {
        for (int dx = 0; dx < 2; dx++) {
          result[(oy * 2 + dy) * new_grid + (ox * 2 + dx)] = gene;
        }
      }
    }
  }

  return result;
}

// Subdivide DNA with bilinear interpolation: sub-cells blend with
// neighboring genes, producing smooth transitions at boundaries.
inline DNA subdivide_dna_interpolate(const DNA& dna) {
  int old_grid = static_cast<int>(std::sqrt(dna.size()));
  int new_grid = old_grid * 2;
  DNA result(new_grid * new_grid);

  auto get = [&](int y, int x) -> const Gene& {
    y = std::clamp(y, 0, old_grid - 1);
    x = std::clamp(x, 0, old_grid - 1);
    return dna[y * old_grid + x];
  };

  auto lerp_color = [](const Color& a, const Color& b, float t) -> Color {
    int r = static_cast<int>(a.r) + static_cast<int>(t * (static_cast<int>(b.r) - a.r) + 0.5f);
    int g = static_cast<int>(a.g) + static_cast<int>(t * (static_cast<int>(b.g) - a.g) + 0.5f);
    int bl = static_cast<int>(a.b) + static_cast<int>(t * (static_cast<int>(b.b) - a.b) + 0.5f);
    return {
        static_cast<uint8_t>(std::clamp(r, 0, 255)),
        static_cast<uint8_t>(std::clamp(g, 0, 255)),
        static_cast<uint8_t>(std::clamp(bl, 0, 255)),
    };
  };

  for (int oy = 0; oy < old_grid; oy++) {
    for (int ox = 0; ox < old_grid; ox++) {
      for (int dy = 0; dy < 2; dy++) {
        for (int dx = 0; dx < 2; dx++) {
          float sx = ox + (dx == 0 ? 0.25f : 0.75f);
          float sy = oy + (dy == 0 ? 0.25f : 0.75f);

          int cx0 = static_cast<int>(std::floor(sx - 0.5f));
          int cy0 = static_cast<int>(std::floor(sy - 0.5f));
          int cx1 = cx0 + 1;
          int cy1 = cy0 + 1;
          float fx = sx - 0.5f - cx0;
          float fy = sy - 0.5f - cy0;

          const Color& c00 = get(cy0, cx0).color;
          const Color& c10 = get(cy0, cx1).color;
          const Color& c01 = get(cy1, cx0).color;
          const Color& c11 = get(cy1, cx1).color;

          Color top = lerp_color(c00, c10, fx);
          Color bot = lerp_color(c01, c11, fx);

          int ny = oy * 2 + dy;
          int nx = ox * 2 + dx;
          result[ny * new_grid + nx] = Gene{lerp_color(top, bot, fy)};
        }
      }
    }
  }

  return result;
}

}  // namespace evo

#endif  // PET_PROJECT_TYPES_H_
