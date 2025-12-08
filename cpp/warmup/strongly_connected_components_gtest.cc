#include <gtest/gtest.h>

#include <utility>
#include <vector>

#include "cpp/warmup/strongly_connected_components.hpp"

using std::pair;
using std::vector;
using warmup::find_scc;

TEST(SCCTest, EmptyGraph) {
  auto res = find_scc(0, {});
  EXPECT_TRUE(res.empty());
}

TEST(SCCTest, SingleVertex) {
  auto res = find_scc(1, {});
  ASSERT_EQ(res.size(), 1);
  EXPECT_EQ(res[0], 1);
}

TEST(SCCTest, IsolatedVertices) {
  // 3 vertices, 0 edges
  auto res = find_scc(3, {});
  ASSERT_EQ(res.size(), 3);
  // Each vertex is its own SCC. IDs might differ but should be distinct.
  EXPECT_NE(res[0], res[1]);
  EXPECT_NE(res[0], res[2]);
  EXPECT_NE(res[1], res[2]);
}

TEST(SCCTest, CycleOfThree) {
  // 3 vertices: 0->1, 1->2, 2->0
  vector<pair<int, int>> edges = {{0, 1}, {1, 2}, {2, 0}};
  auto res = find_scc(3, edges);
  ASSERT_EQ(res.size(), 3);
  // All should have same SCC ID
  EXPECT_EQ(res[0], res[1]);
  EXPECT_EQ(res[1], res[2]);
}

TEST(SCCTest, TwoSCCs) {
  // 4 vertices: 0->1, 1->0, 2->3, 3->2, 1->2
  // SCC1: {0, 1}, SCC2: {2, 3}
  vector<pair<int, int>> edges = {{0, 1}, {1, 0}, {2, 3}, {3, 2}, {1, 2}};
  auto res = find_scc(4, edges);
  ASSERT_EQ(res.size(), 4);
  EXPECT_EQ(res[0], res[1]);
  EXPECT_EQ(res[2], res[3]);
  EXPECT_NE(res[0], res[2]);
}

TEST(SCCTest, ComplexGraph) {
  // 6 vertices
  // 0->1, 1->2, 2->0 (SCC {0,1,2})
  // 3->4, 4->3 (SCC {3,4})
  // 2->3 (edge between SCCs)
  // 4->5 (edge to sink)
  vector<pair<int, int>> edges = {{0, 1}, {1, 2}, {2, 0}, {3, 4},
                                  {4, 3}, {2, 3}, {4, 5}};
  auto res = find_scc(6, edges);
  ASSERT_EQ(res.size(), 6);

  // {0,1,2} same
  EXPECT_EQ(res[0], res[1]);
  EXPECT_EQ(res[1], res[2]);

  // {3,4} same
  EXPECT_EQ(res[3], res[4]);

  // {5} distinct

  EXPECT_NE(res[0], res[3]);
  EXPECT_NE(res[0], res[5]);
  EXPECT_NE(res[3], res[5]);
}

TEST(SCCTest, LinearChain) {
  // 4 vertices: 0->1->2->3
  vector<pair<int, int>> edges = {{0, 1}, {1, 2}, {2, 3}};
  auto res = find_scc(4, edges);
  ASSERT_EQ(res.size(), 4);
  // All distinct
  EXPECT_NE(res[0], res[1]);
  EXPECT_NE(res[1], res[2]);
  EXPECT_NE(res[2], res[3]);
}
