#include <algorithm>
#include <cstddef>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

bool compare_dragons(const pair<int, int> &a, const pair<int, int> &b) {
  return a.first < b.first;
}

// O(n log n)
bool winable(vector<pair<int, int>> dragons, int s) {
  sort(dragons.begin(), dragons.end(), compare_dragons);
  for (auto d : dragons) {
    if (s <= d.first) return false;
    s += d.second;
  }
  return true;
}

// O(n^2)
bool winable_naive(vector<pair<int, int>> dragons, int s) {
  vector<bool> defeated(dragons.size());
  size_t defeated_num = 0;

  while (defeated_num < dragons.size()) {
    bool wining = false;
    for (size_t i = 0; i < dragons.size(); ++i) {
      if (defeated[i]) continue;
      if (dragons[i].first < s) {
        s += dragons[i].second;
        defeated[i] = true;
        wining = true;
        defeated_num++;
        break;
      }
    }

    if (!wining) return false;
  }

  return true;
}

int main(int argc, char *argv[]) {
  int s, n;
  std::cin >> s >> n;
  vector<pair<int, int>> dragons(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> dragons[i].first;
    std::cin >> dragons[i].second;
  }

  if (winable(dragons, s)) {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
