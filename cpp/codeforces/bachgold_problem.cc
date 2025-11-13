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
  int n;
  std::cin >> n;
  std::cout << n / 2 << "\n";

  while (n) {
    if (n > 3) {
      std::cout << 2 << " ";
      n -= 2;
      continue;
    }
    std::cout << n << "\n";
    break;
  }
  return 0;
}
