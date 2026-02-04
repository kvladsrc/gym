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
  int n;
  std::cin >> n;
  if (n % 2) {
    std::cout << -1 << "\n";
    return 0;
  }

  for (int i = n; i > 0; --i) {
    std::cout << i << " ";
  }

  std::cout << "\n";

  return 0;
}
