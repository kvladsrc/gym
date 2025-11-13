#include <algorithm>
#include <iostream>
#include <numeric>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int solve(vector<int> groups) {
  int res = 0;
  while (true) {
    // 1&3 -> 4
    if (groups[0] && groups[2]) {
      groups[0]--;
      groups[2]--;
      res++;
      continue;
    }

    // 1&2 -> 3
    if (groups[0] && groups[1]) {
      groups[0]--;
      groups[1]--;
      groups[2]++;
      continue;
    }

    // 1&1 -> 2
    if (groups[0] > 1) {
      groups[0] -= 2;
      groups[1]++;
      continue;
    }

    // 2&2 -> 4
    if (groups[1] > 1) {
      groups[1] -= 2;
      res++;
      continue;
    }

    // 1&2 -> 3
    if (groups[0] && groups[1]) {
      groups[0]--;
      groups[1]--;
      groups[2]++;
      continue;
    }

    break;
  }

  res += std::accumulate(groups.begin(), groups.end(), 0);

  return res;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> groups(4);
  for (int i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;
    // 1 to 4 -> 1 to 3
    groups[buf - 1]++;
  }

  std::cout << solve(groups) << "\n";

  return 0;
}
