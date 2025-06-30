#include <gtest/gtest.h>

#include <climits>
#include <vector>

#include "cpp/warmup/negative_cycle_detection.hpp"

TEST(NegativeCycleDetectionTest, SingleVertexNoCycle) {
  warmup::adj_matrix dists(1, std::vector<int>(1, INT_MAX));
  dists[0][0] = 0;
  EXPECT_FALSE(warmup::negative_cycle_detection(dists));
}

TEST(NegativeCycleDetectionTest, NoCycle) {
  warmup::adj_matrix dists(3, std::vector<int>(3, INT_MAX));
  for (int i = 0; i < 3; ++i) dists[i][i] = 0;
  dists[0][1] = 3;
  dists[1][2] = 4;
  EXPECT_FALSE(warmup::negative_cycle_detection(dists));
}

TEST(NegativeCycleDetectionTest, NegativeCycleSimple) {
  warmup::adj_matrix dists(3, std::vector<int>(3, INT_MAX));
  for (int i = 0; i < 3; ++i) dists[i][i] = 0;
  dists[0][1] = 1;
  dists[1][2] = -3;
  dists[2][0] = 1;
  EXPECT_TRUE(warmup::negative_cycle_detection(dists));
}

TEST(NegativeCycleDetectionTest, SelfLoopNegative) {
  warmup::adj_matrix dists(1, std::vector<int>(1, INT_MAX));
  dists[0][0] = -1;
  EXPECT_TRUE(warmup::negative_cycle_detection(dists));
}

TEST(NegativeCycleDetectionTest, NegativeWeightButNoCycle) {
  warmup::adj_matrix dists(2, std::vector<int>(2, INT_MAX));
  for (int i = 0; i < 2; ++i) dists[i][i] = 0;
  dists[0][1] = -5;
  EXPECT_FALSE(warmup::negative_cycle_detection(dists));
}

TEST(NegativeCycleDetectionTest, DisconnectedOneComponentWithNegativeCycle) {
  warmup::adj_matrix dists(4, std::vector<int>(4, INT_MAX));
  for (int i = 0; i < 4; ++i) dists[i][i] = 0;
  dists[0][1] = 2;
  dists[2][3] = -1;
  dists[3][2] = -1;
  EXPECT_TRUE(warmup::negative_cycle_detection(dists));
}
