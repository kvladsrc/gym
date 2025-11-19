package world

import (
	"os"
	"strings"

	"src/ripples_cli/pkg/model"

	"github.com/awalterschulze/gographviz"
	"github.com/awalterschulze/gographviz/ast"
)

// LoadWorld loads the game world from a DOT file.
func LoadWorld(path string) (map[string]*model.Node, error) {
	bytes, err := os.ReadFile(path)
	if err != nil {
		return nil, err
	}

	graphAst, err := gographviz.Parse(bytes)
	if err != nil {
		return nil, err
	}

	nodes := make(map[string]*model.Node)
	edges := make(map[string][]string) // src -> []dst

	// Manual AST traversal
	for _, stmt := range graphAst.StmtList {
		switch s := stmt.(type) {
		case *ast.NodeStmt:
			node := parseNode(s)
			nodes[node.ID] = node
		case *ast.EdgeStmt:
			src, dst := parseEdge(s)
			if src != "" && dst != "" {
				if !contains(edges[src], dst) {
					edges[src] = append(edges[src], dst)
				}
				// Make edges bidirectional
				if !contains(edges[dst], src) {
					edges[dst] = append(edges[dst], src)
				}
			}
		}
	}

	// Link edges
	for src, dsts := range edges {
		if node, ok := nodes[src]; ok {
			node.Adjacent = append(node.Adjacent, dsts...)
		}
	}

	return nodes, nil
}

func contains(slice []string, item string) bool {
	for _, s := range slice {
		if s == item {
			return true
		}
	}
	return false
}

func parseNode(s *ast.NodeStmt) *model.Node {
	id := strings.Trim(string(s.NodeID.ID), "\"")
	node := &model.Node{
		ID:       id,
		Adjacent: []string{},
	}

	for key, val := range s.Attrs.GetMap() {
		val = strings.Trim(val, "\"")
		switch key {
		case "label":
			node.Name = val
		case "description":
			node.Description = val
		case "type":
			node.Type = model.NodeType(val)
		}
	}

	if node.Type == "" {
		node.Type = model.LocationType
	}
	return node
}

func parseEdge(s *ast.EdgeStmt) (string, string) {
	srcID := ""
	if nodeID, ok := s.Source.(*ast.NodeID); ok {
		srcID = strings.Trim(string(nodeID.ID), "\"")
	}

	dstID := ""
	if len(s.EdgeRHS) > 0 {
		rhs := s.EdgeRHS[0]
		if nodeID, ok := rhs.Destination.(*ast.NodeID); ok {
			dstID = strings.Trim(string(nodeID.ID), "\"")
		}
	}
	return srcID, dstID
}
