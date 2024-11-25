#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

// Tetrahedron. Tetrahedron has 4 triangular faces.
// Cube. Cube has 6 square faces.
// Octahedron. Octahedron has 8 triangular faces.
// Dodecahedron. Dodecahedron has 12 pentagonal faces.
// Icosahedron. Icosahedron has 20 triangular faces.

int faces_num(const string &pol) {
  if (pol == "Tetrahedron") return 4;
  if (pol == "Cube") return 6;
  if (pol == "Octahedron") return 8;
  if (pol == "Dodecahedron") return 12;
  if (pol == "Icosahedron") return 20;

  return 0;
}

int main(int argc, char *argv[]) {
  int n, res = 0;
  std::cin >> n;
  while (n) {
    string buf;
    std::cin >> buf;
    res += faces_num(buf);
    n--;
  }

  std::cout << res << "\n";

  return 0;
}
