#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

using std::abs;
using std::gcd;
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

int gcd(int a, int b) {
  if (a % b == 0) {
    return b;
  }

  return gcd(b, a % b);
}

void swap(int& a, int& b) {
  int buf = a;
  a = b;
  b = buf;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    int minimal_pos = 0;
    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
      if (a[minimal_pos] > a[i]) {
        minimal_pos = i;
      }
    }

    vector<pair<pair<int, int>, pair<int, int>>> ops;

    // a[0] contain the minimal element.
    if (minimal_pos != 0) {
      swap(a[minimal_pos], a[0]);
      ops.push_back({{1, minimal_pos + 1}, {a[0], a[minimal_pos]}});
    }

    for (int i = 1; i < n; ++i) {
      bool add_step = false;
      while (gcd(a[i], a[i - 1]) != 1) {
        a[i]++;
        add_step = true;
      }

      if (add_step) {
        ops.push_back({{1, i + 1}, {a[0], a[i]}});
      }
    }

    std::cout << ops.size() << "\n";
    for (auto p : ops) {
      std::cout << p.first.first << " ";
      std::cout << p.first.second << " ";
      std::cout << p.second.first << " ";
      std::cout << p.second.second << " ";
      std::cout << "\n";
    }
  }

  return 0;
}
