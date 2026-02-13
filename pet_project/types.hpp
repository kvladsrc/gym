#ifndef PET_PROJECT_TYPES_H_
#define PET_PROJECT_TYPES_H_

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <random>
#include <vector>

namespace evo {

struct Vertex {
  float x;
  float y;
};

struct Color {
  uint8_t r;
  uint8_t g;
  uint8_t b;
  uint8_t a;
};

struct Gene {
  Vertex vertices[3];
  Color color;
  bool enabled;
};

using DNA = std::vector<Gene>;

struct Individual {
  DNA dna;
  float fitness;
};

inline Gene random_gene(std::mt19937& rng, float max_radius) {
  std::uniform_real_distribution<float> coord(0.0f, 1.0f);
  std::uniform_real_distribution<float> angle(0.0f,
                                              2.0f * 3.14159265358979f);
  std::uniform_real_distribution<float> radius(0.0f, max_radius);
  std::uniform_int_distribution<int> col(0, 255);
  std::uniform_int_distribution<int> coin(0, 1);

  Gene g{};

  // First vertex: fully random
  g.vertices[0].x = coord(rng);
  g.vertices[0].y = coord(rng);

  // Remaining vertices: within max_radius of the first vertex
  for (int i = 1; i < 3; i++) {
    float r = radius(rng);
    float a = angle(rng);
    g.vertices[i].x = std::clamp(g.vertices[0].x + r * std::cos(a), 0.0f, 1.0f);
    g.vertices[i].y = std::clamp(g.vertices[0].y + r * std::sin(a), 0.0f, 1.0f);
  }

  g.color.r = col(rng);
  g.color.g = col(rng);
  g.color.b = col(rng);
  g.color.a = 255;
  g.enabled = coin(rng);

  return g;
}

inline DNA random_dna(int length, std::mt19937& rng, float max_radius) {
  DNA dna(length);

  for (auto& gene : dna) {
    gene = random_gene(rng, max_radius);
  }

  return dna;
}

}  // namespace evo

#endif  // PET_PROJECT_TYPES_H_
