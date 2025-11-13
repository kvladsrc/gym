#include <vector>

#include "cpp/warmup/minimum_spanning_tree.hpp"
#include "gtest/gtest.h"

namespace warmup {

TEST(MinimumSpanningTreeTest, BasicTest) {
  const int v = 4;
  std::vector<edge> edges = {
      {10, {0, 1}}, {6, {0, 2}}, {5, {0, 3}}, {15, {1, 3}}, {4, {2, 3}}};
  const std::vector<edge> expected_mst = {
      {4, {2, 3}}, {5, {0, 3}}, {10, {0, 1}}};
  auto result_mst = kruskal(v, edges);
  int total_weight = 0;
  for (const auto &e : result_mst) {
    total_weight += e.first;
  }
  ASSERT_EQ(total_weight, 19);
}

TEST(MinimumSpanningTreeTest, EmptyGraph) {
  const int v = 0;
  std::vector<edge> edges;
  auto result_mst = kruskal(v, edges);
  ASSERT_TRUE(result_mst.empty());
}

TEST(MinimumSpanningTreeTest, SingleVertex) {
  const int v = 1;
  std::vector<edge> edges;
  auto result_mst = kruskal(v, edges);
  ASSERT_TRUE(result_mst.empty());
}

TEST(MinimumSpanningTreeTest, DisconnectedGraph) {
  const int v = 5;
  std::vector<edge> edges = {{1, {0, 1}}, {2, {2, 3}}};
  auto result_mst = kruskal(v, edges);
  int total_weight = 0;
  for (const auto &e : result_mst) {
    total_weight += e.first;
  }
  ASSERT_EQ(total_weight, 3);
  ASSERT_EQ(result_mst.size(), 2);
}

TEST(MinimumSpanningTreeTest, AlreadySortedEdges) {
  const int v = 4;
  std::vector<edge> edges = {
      {4, {2, 3}}, {5, {0, 3}}, {6, {0, 2}}, {10, {0, 1}}, {15, {1, 3}}};
  auto result_mst = kruskal(v, edges);
  int total_weight = 0;
  for (const auto &e : result_mst) {
    total_weight += e.first;
  }
  ASSERT_EQ(total_weight, 19);
}

TEST(MinimumSpanningTreeTest, AllWeightsSame) {
  const int v = 4;
  std::vector<edge> edges = {
      {1, {0, 1}}, {1, {1, 2}}, {1, {2, 3}}, {1, {3, 0}}};
  auto result_mst = kruskal(v, edges);
  ASSERT_EQ(result_mst.size(), 3);
  int total_weight = 0;
  for (const auto &e : result_mst) {
    total_weight += e.first;
  }
  ASSERT_EQ(total_weight, 3);
}

}  // namespace warmup
