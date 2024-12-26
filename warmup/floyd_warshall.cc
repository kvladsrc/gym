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

  // Init by copying graph.
  vector<vector<int>> dists(graph);

  // Searching for a shortests pathes between any two verticies.
  for (int iter = 0; iter < n; ++iter) {
    for (int from = 0; from < n; ++from) {
      for (int to = 0; to < n; ++to) {
        if (to == from) {
          continue;
        }
        for (int by = 0; by < n; ++by) {
          if (by == to || by == from) {
            continue;
          }

          dists[from][to] =
              std::min(dists[from][to], dists[from][by] + dists[by][to]);
        }
      }
    }
  }

  for (int i = 1; i < n; ++i) {
    std::cout << "0 to " << i << " dist: " << dists[0][i] << "\n";
  }

  return 0;
}
