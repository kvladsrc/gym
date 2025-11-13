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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
    }

    sort(a.begin(), a.end());

    if (a[0] == a[n - 1]) {
      std::cout << -1 << std::endl;
      continue;
    }

    vector<int> b;
    vector<int> c;
    for (int i : a) {
      if (i == a[n - 1]) {
        c.push_back(i);
      } else {
        b.push_back(i);
      }
    }

    std::cout << b.size() << " " << c.size() << std::endl;
    for (int i : b) {
      std::cout << i << " ";
    }
    std::cout << std::endl;
    for (int i : c) {
      std::cout << i << " ";
    }
    std::cout << std::endl;
  }

  return 0;
}
