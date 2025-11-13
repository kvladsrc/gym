#include <algorithm>
#include <cstddef>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int solve(vector<int> available, int n) {
  sort(available.begin(), available.end());
  int best = available[available.size() - 1] - available[0];
  n--;  // index

  for (size_t i = 0; i < available.size(); ++i) {
    if (i + n >= available.size()) break;
    if (available[i + n] - available[i] < best) {
      best = available[i + n] - available[i];
    }
  }

  return best;
}

int main(int argc, char *argv[]) {
  int n, m;
  std::cin >> n >> m;
  vector<int> available(m);
  for (int i = 0; i < m; ++i) {
    std::cin >> available[i];
  }

  std::cout << solve(available, n) << "\n";

  return 0;
}
