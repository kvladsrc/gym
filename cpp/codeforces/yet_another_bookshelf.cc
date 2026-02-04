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
    int books = 0;
    for (auto& i : a) {
      std::cin >> i;
      books += i;
    }

    int gaps = n - books;
    for (auto i : a) {
      if (i) break;
      gaps--;
    }

    for (int i = n - 1; i >= 0; --i) {
      if (a[i]) break;
      gaps--;
    }

    std::cout << gaps << "\n";
  }

  return 0;
}
