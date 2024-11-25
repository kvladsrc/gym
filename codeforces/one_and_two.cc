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

int binary_search(vector<int> &s, int a) {
  if (a == s[0]) return 0;

  int l = 0, r = s.size() - 1;

  while (r - l > 1) {
    int mid = (l + r) / 2;

    if (s[mid] >= a) {
      r = mid;
    } else {
      l = mid;
    }
  }

  return r;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> two_sumamnds(n);
    int buf;
    std::cin >> buf;
    if (buf == 2) {
      two_sumamnds[0] = 1;
    } else {
      two_sumamnds[0] = 0;
    }

    for (int i = 1; i < n; ++i) {
      std::cin >> buf;
      if (buf == 2) {
        two_sumamnds[i] = (i > 0 ? two_sumamnds[i - 1] + 1 : 1);
      } else {
        two_sumamnds[i] = (i > 0 ? two_sumamnds[i - 1] : 0);
      }
    }

    if (two_sumamnds.back() % 2) {
      std::cout << -1 << "\n";
    } else {
      int half = two_sumamnds.back() / 2;
      std::cout << binary_search(two_sumamnds, half) + 1 << "\n";
    }
  }

  return 0;
}
