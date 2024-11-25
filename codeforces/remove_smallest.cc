#include <algorithm>
#include <cstddef>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

bool solveble(vector<int> &a) {
  sort(a.begin(), a.end());
  if (a.size() == 1)
    return true;
  for (size_t i = 1; i < a.size(); ++i) {
    if (a[i] - a[i - 1] > 1)
      return false;
  }
  return true;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<vector<int>> arrays(n);
  for (int i = 0; i < n; ++i) {
    int elems;
    std::cin >> elems;
    arrays[i].resize(elems);
    for (int j = 0; j < elems; ++j) {
      std::cin >> arrays[i][j];
    }
  }

  for (auto a : arrays) {
    if (solveble(a)) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
