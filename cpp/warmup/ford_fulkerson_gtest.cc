#include <gtest/gtest.h>

#include <cstddef>
#include <vector>

#include "cpp/warmup/ford_fulkerson.hpp"

namespace warmup {
namespace {

struct FlowNetwork {
  explicit FlowNetwork(std::size_t vertex_count)
      : capacities(vertex_count, std::vector<int>(vertex_count)),
        graph(vertex_count) {}

  void AddEdge(std::size_t from, std::size_t to, int capacity) {
    capacities[from][to] += capacity;
    graph[from].push_back(to);
    graph[to].push_back(from);
  }

  adj_matrix capacities;
  adj_list graph;
};

TEST(FordFulkersonTest, SourceEqualsSink) {
  FlowNetwork network(1);

  EXPECT_EQ(ford_fulkerson(network.capacities, network.graph, 0, 0), 0);
}

TEST(FordFulkersonTest, DisconnectedSink) {
  FlowNetwork network(3);
  network.AddEdge(0, 1, 10);

  EXPECT_EQ(ford_fulkerson(network.capacities, network.graph, 0, 2), 0);
}

TEST(FordFulkersonTest, SinglePathUsesBottleneckCapacity) {
  FlowNetwork network(4);
  network.AddEdge(0, 1, 7);
  network.AddEdge(1, 2, 3);
  network.AddEdge(2, 3, 5);

  EXPECT_EQ(ford_fulkerson(network.capacities, network.graph, 0, 3), 3);
}

TEST(FordFulkersonTest, SumsFlowAcrossIndependentPaths) {
  FlowNetwork network(4);
  network.AddEdge(0, 1, 4);
  network.AddEdge(1, 3, 4);
  network.AddEdge(0, 2, 6);
  network.AddEdge(2, 3, 6);

  EXPECT_EQ(ford_fulkerson(network.capacities, network.graph, 0, 3), 10);
}

TEST(FordFulkersonTest, FindsMaximumFlowInCanonicalNetwork) {
  FlowNetwork network(6);
  network.AddEdge(0, 1, 16);
  network.AddEdge(0, 2, 13);
  network.AddEdge(1, 2, 10);
  network.AddEdge(2, 1, 4);
  network.AddEdge(1, 3, 12);
  network.AddEdge(3, 2, 9);
  network.AddEdge(2, 4, 14);
  network.AddEdge(4, 3, 7);
  network.AddEdge(3, 5, 20);
  network.AddEdge(4, 5, 4);

  EXPECT_EQ(ford_fulkerson(network.capacities, network.graph, 0, 5), 23);
}

TEST(FordFulkersonTest, CanRerouteFlowThroughResidualReverseEdge) {
  FlowNetwork network(6);
  network.AddEdge(0, 1, 1);
  network.AddEdge(0, 2, 1);
  network.AddEdge(1, 3, 1);
  network.AddEdge(1, 4, 1);
  network.AddEdge(2, 3, 1);
  network.AddEdge(3, 5, 1);
  network.AddEdge(4, 5, 1);

  EXPECT_EQ(ford_fulkerson(network.capacities, network.graph, 0, 5), 2);
}

TEST(FordFulkersonTest, DoesNotModifyCallerCapacities) {
  FlowNetwork network(2);
  network.AddEdge(0, 1, 8);
  const adj_matrix original_capacities = network.capacities;

  EXPECT_EQ(ford_fulkerson(network.capacities, network.graph, 0, 1), 8);
  EXPECT_EQ(network.capacities, original_capacities);
}

}  // namespace
}  // namespace warmup
