#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    sort(a.begin(), a.end());

    if (n == 2) {
      for (auto i : a) {
        std::cout << i << " ";
      }
      std::cout << "\n";
      continue;
    }

    int min_diff = std::numeric_limits<int>::max();
    int pos = 0;
    for (int i = 1; i < n; ++i) {
      int diff = a[i] - a[i - 1];
      if (diff < min_diff) {
        min_diff = diff;
        pos = i;
      }
    }

    vector<int> b;
    for (int i = pos; i < n; ++i) {
      b.push_back(a[i]);
    }
    for (int i = 0; i < pos; ++i) {
      b.push_back(a[i]);
    }

    for (auto i : b) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
