#include <gtest/gtest.h>

#include <climits>
#include <vector>

#include "cpp/warmup/floyd_warshall.hpp"

TEST(FloydWarshallTest, EmptyGraph) {
  warmup::adj_matrix dists(1, std::vector<int>(1, INT_MAX));
  dists[0][0] = 0;
  warmup::floyd_warshall(dists);
  EXPECT_EQ(dists[0][0], 0);
}

TEST(FloydWarshallTest, SingleEdge) {
  warmup::adj_matrix dists(2, std::vector<int>(2, INT_MAX));
  dists[0][0] = 0;
  dists[1][1] = 0;
  dists[0][1] = 10;
  warmup::floyd_warshall(dists);
  EXPECT_EQ(dists[0][0], 0);
  EXPECT_EQ(dists[0][1], 10);
  EXPECT_EQ(dists[1][0], INT_MAX);
  EXPECT_EQ(dists[1][1], 0);
}

TEST(FloydWarshallTest, MultiplePaths) {
  warmup::adj_matrix dists(3, std::vector<int>(3, INT_MAX));
  for (int i = 0; i < 3; ++i) dists[i][i] = 0;
  dists[0][1] = 1;
  dists[1][2] = 2;
  dists[0][2] = 5;
  warmup::floyd_warshall(dists);
  EXPECT_EQ(dists[0][0], 0);
  EXPECT_EQ(dists[0][1], 1);
  EXPECT_EQ(dists[0][2], 3);  // 0->1->2 is shorter than 0->2
  EXPECT_EQ(dists[1][0], INT_MAX);
  EXPECT_EQ(dists[1][1], 0);
  EXPECT_EQ(dists[1][2], 2);
  EXPECT_EQ(dists[2][0], INT_MAX);
  EXPECT_EQ(dists[2][1], INT_MAX);
  EXPECT_EQ(dists[2][2], 0);
}

TEST(FloydWarshallTest, NegativeWeights) {
  warmup::adj_matrix dists(2, std::vector<int>(2, INT_MAX));
  for (int i = 0; i < 2; ++i) dists[i][i] = 0;
  dists[0][1] = -3;
  dists[1][0] = 5;
  warmup::floyd_warshall(dists);
  EXPECT_EQ(dists[0][0], 0);
  EXPECT_EQ(dists[0][1], -3);
  EXPECT_EQ(dists[1][0], 5);
  EXPECT_EQ(dists[1][1], 0);
}

TEST(FloydWarshallTest, UnreachableVertices) {
  warmup::adj_matrix dists(3, std::vector<int>(3, INT_MAX));
  for (int i = 0; i < 3; ++i) dists[i][i] = 0;
  dists[0][1] = 10;
  warmup::floyd_warshall(dists);
  EXPECT_EQ(dists[0][0], 0);
  EXPECT_EQ(dists[0][1], 10);
  EXPECT_EQ(dists[0][2], INT_MAX);
  EXPECT_EQ(dists[1][0], INT_MAX);
  EXPECT_EQ(dists[1][1], 0);
  EXPECT_EQ(dists[1][2], INT_MAX);
  EXPECT_EQ(dists[2][0], INT_MAX);
  EXPECT_EQ(dists[2][1], INT_MAX);
  EXPECT_EQ(dists[2][2], 0);
}
