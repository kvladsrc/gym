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
    int n, k;
    std::cin >> n >> k;

    int l = 1;
    while (k) {
      std::cout << l++ << " ";
      k--;
    }

    while (n >= l) {
      std::cout << n-- << " ";
    }

    std::cout << "\n";
  }

  return 0;
}
