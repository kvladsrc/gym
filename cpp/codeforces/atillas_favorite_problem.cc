#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
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
    int max_val = 0;
    while (n--) {
      char c;
      std::cin >> c;
      if (c - 'a' > max_val) max_val = c - 'a';
    }
    std::cout << max_val + 1 << "\n";
  }
  return 0;
}
