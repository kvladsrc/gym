#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::min;
using std::string;
using std::vector;

using adj_matrix = vector<vector<int>>;

int main(int /*argc*/, char* /*argv*/[]) {
  size_t n = 0;
  std::cin >> n;

  adj_matrix dists(n, vector<int>(n, INT_MAX));
  for (size_t a = 0; a < n; ++a) {
    dists[a][a] = 0;
  }

  int e = 0;
  std::cin >> e;
  while ((e--) != 0) {
    int a = 0;
    int b = 0;
    int d = 0;
    std::cin >> a >> b >> d;
    dists[a][b] = d;
  }

  // Searching for a shortests pathes between any two verticies.
  // TODO(zjgkkn): Negative loop detection (dists[a][a] < 0 for some
  // a).
  for (size_t k = 0; k < n; ++k) {
    for (size_t a = 0; a < n; ++a) {
      for (size_t b = 0; b < n; ++b) {
        if (dists[a][k] != INT_MAX && dists[k][b] != INT_MAX) {
          dists[a][b] = min(dists[a][b], dists[a][k] + dists[k][b]);
        }
      }
    }
  }

  for (size_t a = 0; a < n; ++a) {
    for (size_t b = 0; b < n; ++b) {
      std::cout << a << " " << b << " ";
      if (dists[a][b] == INT_MAX) {
        std::cout << "INF" << "\n";
      } else {
        std::cout << dists[a][b] << "\n";
      }
    }
  }

  return 0;
}
