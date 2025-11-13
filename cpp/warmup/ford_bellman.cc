#include <algorithm>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  const int inf = 1000000000;
  vector<vector<int>> graph(n, vector<int>(n, inf));

  int e = 0;
  std::cin >> e;
  while ((e--) != 0) {
    int a = 0;
    int b = 0;
    int d = 0;
    std::cin >> a >> b >> d;
    graph[a][b] = d;
  }

  // Searching for shortest path from 0 to all other verticies.
  // Init dists.
  vector<int> dists(n);
  dists[0] = 0;
  for (int i = 1; i < n; ++i) {
    dists[i] = graph[0][i];
  }

  // Let's go!
  for (int iter = 0; iter < n; ++iter) {
    for (int to = 1; to < n; ++to) {
      for (int by = 1; by < n; ++by) {
        if (to == by) {
          continue;
        }
        dists[to] = std::min(dists[to], dists[by] + graph[by][to]);
      }
    }
  }

  for (int i = 1; i < n; ++i) {
    std::cout << "0 to " << i << " dist: " << dists[i] << "\n";
  }

  return 0;
}
