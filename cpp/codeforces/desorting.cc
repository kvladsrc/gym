#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

    int min_gap = a.back() - a.front();
    for (int i = 1; i < n; ++i) {
      if ((a[i] - a[i - 1]) < min_gap) {
        min_gap = a[i] - a[i - 1];
      }
    }

    if (min_gap < 0) {
      std::cout << 0 << "\n";
    } else {
      std::cout << (min_gap / 2) + 1 << "\n";
    }
  }

  return 0;
}
