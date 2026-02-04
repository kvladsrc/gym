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
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;

  map<int64_t, int> m;
  map<int64_t, bool> has_arrow;
  for (int i = 0; i < n; ++i) {
    int64_t buf;
    std::cin >> buf;
    m[buf]++;
    has_arrow[buf] = false;
  }

  // We need to find the first element, then whole sequense it
  // determined. First element is the element without incoming arrows.

  for (auto p : m) {
    if (m.find(p.first * 2) != m.end()) {
      has_arrow[p.first * 2] = true;
    }

    if (p.first % 3 == 0 && m.find(p.first / 3) != m.end()) {
      has_arrow[p.first / 3] = true;
    }
  }

  int64_t first = 0;
  for (auto p : has_arrow) {
    if (p.second) {
      continue;
    }
    first = p.first;
    break;
  }

  for (int i = 0; i < n; ++i) {
    std::cout << first << " ";
    if (m[first * 2] > 0) {
      first = first * 2;
      continue;
    }

    if (first % 3 == 0 && m[first / 3] > 0) {
      first = first / 3;
    }
  }

  return 0;
}
