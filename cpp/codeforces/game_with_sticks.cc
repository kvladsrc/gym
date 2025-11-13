#include <algorithm>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n, m;
  std::cin >> n >> m;
  if (min(n, m) % 2)
    std::cout << "Akshat"
              << "\n";
  else
    std::cout << "Malvika"
              << "\n";

  return 0;
}
