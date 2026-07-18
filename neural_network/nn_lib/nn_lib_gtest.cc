#include <gtest/gtest.h>

#include <algorithm>
#include <cstddef>
#include <numeric>
#include <random>
#include <vector>

#include "neural_network/nn_lib/nn_lib.h"

namespace {

V random_values(std::mt19937& rng, std::size_t size) {
  std::uniform_real_distribution<double> distribution(-10.0, 10.0);
  V values(size);
  std::generate(values.begin(), values.end(),
                [&] { return distribution(rng); });
  return values;
}

std::vector<std::size_t> sorted_indices(const V& values) {
  std::vector<std::size_t> indices(values.size());
  std::iota(indices.begin(), indices.end(), 0);
  std::stable_sort(indices.begin(), indices.end(),
                   [&](std::size_t lhs, std::size_t rhs) {
                     return values[lhs] < values[rhs];
                   });
  return indices;
}

}  // namespace

TEST(ActivateTest, ActivatesRandomVector) {
  std::mt19937 rng(42);
  V values = random_values(rng, 128);
  V expected = values;
  std::transform(expected.begin(), expected.end(), expected.begin(),
                 [](double value) { return std::max(value, 0.0); });

  activate(values);

  EXPECT_EQ(values, expected);
}

TEST(DropoutTest, DropsOrScalesRandomValues) {
  std::mt19937 rng(42);
  V values = random_values(rng, 256);
  const V original = values;
  constexpr double keep_probability = 0.25;

  dropout(values, keep_probability);

  for (std::size_t i = 0; i < values.size(); ++i) {
    EXPECT_TRUE(values[i] == 0.0 ||
                values[i] == original[i] / keep_probability);
  }
}

TEST(SoftmaxTest, PreservesOrderAndSumsToOne) {
  std::mt19937 rng(42);
  V values = random_values(rng, 128);
  const auto logits_order = sorted_indices(values);

  softmax(values);

  EXPECT_EQ(sorted_indices(values), logits_order);
  EXPECT_NEAR(std::accumulate(values.begin(), values.end(), 0.0), 1.0, 1e-12);
}
