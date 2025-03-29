#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char * /*argv*/[]) {
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int k = 0;
    std::cin >> n >> k;

    vector<vector<char>> orig(n, vector<char>(n));
    for (auto &r : orig) {
      for (auto &c : r) {
        std::cin >> c;
      }
    }

    auto scaled = n / k;

    vector<vector<char>> res(scaled, vector<char>(scaled));

    for (auto y = 0; y < scaled; ++y) {
      for (auto x = 0; x < scaled; ++x) {
        res[y][x] = orig[y * k][x * k];
      }
    }

    for (auto r : res) {
      for (auto c : r) {
        std::cout << c;
      }
      std::cout << "\n";
    }
  }

  return 0;
}
