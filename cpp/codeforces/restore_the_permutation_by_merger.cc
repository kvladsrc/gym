#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
    unordered_map<int, bool> m;
    for (int i = 0; i < 2 * n; ++i) {
      int buf;
      std::cin >> buf;
      if (m[buf]) continue;
      std::cout << buf << ' ';
      m[buf] = true;
    }
    std::cout << "\n";
  }
  return 0;
}
