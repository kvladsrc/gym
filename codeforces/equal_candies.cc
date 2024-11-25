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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    int total = 0;
    int min = 1000000000;
    for (int i = 0; i < n; i++) {
      int num;
      std::cin >> num;
      if (num < min) min = num;
      total += num;
    }
    std::cout << total - n * min << "\n";
  }
  return 0;
}
